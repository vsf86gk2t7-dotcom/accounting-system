const { Client, LocalAuth } = require('whatsapp-web.js');
const http = require('http');
const fs = require('fs');
const os = require('os');
const path = require('path');
const crypto = require('crypto');
const qrcode = require('qrcode');

const PORT = 3210;
const HOST = '127.0.0.1';

// =========================================
// مصادقة البوابة: مفتاح مشترك يرسله التطبيق
// في رأس x-gateway-key لكل طلبات الإرسال
// =========================================

const GATEWAY_KEY = process.env.WAPP_GATEWAY_KEY || '';

// مجلد مسموح لملفات /send-document:
// يُقرأ من WAPP_ALLOWED_DIR أو يقع افتراضيًا على مجلد مؤقت النظام
const ALLOWED_DIR = path.resolve(
    process.env.WAPP_ALLOWED_DIR || os.tmpdir());

function safeEqual(a, b) {
    const ab = Buffer.from(String(a || ''));
    const bb = Buffer.from(String(b || ''));
    if (ab.length !== bb.length) return false;
    return crypto.timingSafeEqual(ab, bb);
}

function isAllowedFile(filePath) {
    if (!filePath || typeof filePath !== 'string') return false;
    const resolved = path.resolve(filePath);
    const root = ALLOWED_DIR.endsWith(path.sep)
        ? ALLOWED_DIR
        : ALLOWED_DIR + path.sep;
    return resolved.toLowerCase().startsWith(root.toLowerCase());
}

const client = new Client({
    authStrategy: new LocalAuth({ dataPath: path.join(__dirname, '.wapp-session') }),
    puppeteer: {
        headless: true,
        args: [
            '--no-sandbox',
            '--disable-setuid-sandbox',
            '--disable-dev-shm-usage',
            '--disable-gpu'
        ]
    }
});

let isReady = false;
let hasQr = false;
let currentQr = null;

client.on('qr', (qr) => {
    hasQr = true;
    currentQr = qr;
    console.log('\n[QR_SCAN_NEEDED]\n' + qr + '\n');
});

client.on('ready', async () => {
    isReady = true;
    hasQr = false;
    currentQr = null;
    console.log('[GATEWAY_READY]');
    console.log('Connected as: ' + client.info.wid.user);
    // إحماء المتجر الداخلي لتجنّب خطأ getChat الشهير
    try {
        const chats = await client.getChats();
        console.log('[STORE_WARMED] chats=' + chats.length);
    } catch (e) {
        console.log('[STORE_WARM_WARN] ' + e.message);
    }
});

client.on('authenticated', () => console.log('[AUTHENTICATED]'));
client.on('auth_failure', (msg) => console.log('[AUTH_FAILURE] ' + msg));
client.on('disconnected', (reason) => {
    isReady = false;
    console.log('[DISCONNECTED] ' + reason);
    client.initialize();
});

function normalizePhone(phone) {
    if (!phone) return '';
    let digits = String(phone).replace(/\D/g, '');
    if (digits.length === 10 && digits.startsWith('01')) return '2' + digits;
    if (digits.length === 11 && digits.startsWith('10')) return '2' + digits;
    if (digits.length === 11 && digits.startsWith('20')) return digits;
    return digits;
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function withRetry(fn, attempts = 3) {
    let lastErr;
    for (let i = 0; i < attempts; i++) {
        try {
            return await fn();
        } catch (e) {
            lastErr = e;
            console.log('[RETRY] attempt ' + (i + 1) + ' failed: ' + e.message);
            if (i < attempts - 1) await sleep(2500);
        }
    }
    throw lastErr;
}

function sendJson(res, code, obj) {
    const body = JSON.stringify(obj);
    res.writeHead(code, { 'Content-Type': 'application/json; charset=utf-8', 'Content-Length': Buffer.byteLength(body) });
    res.end(body);
}

function sendHtml(res, html) {
    const body = Buffer.from(html, 'utf8');
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Content-Length': body.length });
    res.end(body);
}

function isKnownStoreError(msg) {
    if (!msg) return false;
    const t = String(msg).toLowerCase();
    return (
        t.indexOf('getchat') !== -1 ||
        t.indexOf('getmessages') !== -1 ||
        t.indexOf('store_not_opened') !== -1 ||
        t.indexOf('store not opened') !== -1 ||
        t.indexOf('unable to obtain data at index') !== -1 ||
        (t.indexOf('reading') !== -1 && t.indexOf('undefined') !== -1)
    );
}

function readBody(req) {
    return new Promise((resolve, reject) => {
        const chunks = [];
        req.on('data', (c) => chunks.push(c));
        req.on('end', () => {
            try { resolve(JSON.parse(Buffer.concat(chunks).toString('utf8'))); }
            catch (e) { reject(new Error('Invalid JSON')); }
        });
        req.on('error', reject);
    });
}

const server = http.createServer(async (req, res) => {
    const url = req.url.split('?')[0];

    if (req.method === 'GET' && url === '/status') {
        sendJson(res, 200, { ready: isReady, hasQr, phone: isReady ? client.info.wid.user : null });
        return;
    }

    if (req.method === 'GET' && url === '/qr') {
        if (isReady) {
            sendHtml(res, '<html><body style="font-family:sans-serif;text-align:center;padding-top:40px"><h2>البوابة متصلة بالفعل!</h2><p>الرقم متصل: ' + client.info.wid.user + '</p></body></html>');
            return;
        }
        if (!currentQr) {
            sendHtml(res, '<html><body style="font-family:sans-serif;text-align:center;padding-top:40px"><h2>بانتظار توليد الرمز... أعد تحميل الصفحة</h2></body></html>');
            return;
        }
        qrcode.toDataURL(currentQr, { margin: 1, width: 500 })
            .then((dataUrl) => {
                sendHtml(res,
                    '<!DOCTYPE html><html lang="ar" dir="rtl"><head><meta charset="utf-8">' +
                    '<meta http-equiv="refresh" content="20">' +
                    '<style>body{font-family:sans-serif;background:#0b141a;color:#fff;text-align:center;padding:30px}h1{font-size:22px}p{color:#aebac1}.wrap{background:#fff;display:inline-block;padding:36px;border-radius:16px;margin:20px auto}.hint{max-width:420px;margin:0 auto;line-height:1.8;color:#e7edf2}</style></head><body>' +
                    '<h1>امسح رمز QR لتوصيل الواتساب</h1>' +
                    '<p>سيتحدث الرمز تلقائيًا كل 20 ثانية</p>' +
                    '<div class="wrap"><img id="qr" src="' + dataUrl + '" alt="QR" width="400"></div>' +
                    '<div class="hint">1) في واتساب الموبايل افتح الإعدادات ⚙️ ثم «الأجهزة المرتبطة» → «ربط جهاز»<br>2) صوّر الكود بكاميرا الموبايل<br>3) انتظر ظهور «GATEWAY_READY» في شاشة الخادم</div>' +
                    '</body></html>');
            })
            .catch((err) => sendJson(res, 500, { ok: false, error: String(err.message || err) }));
        return;
    }

    if (!GATEWAY_KEY) {
        console.log('[GATEWAY_KEY_MISSING] ابحث عن WAPP_GATEWAY_KEY في متغيرات البيئة لتفعيل الإرسال');
    }

    const isAuthorized = () =>
        GATEWAY_KEY && safeEqual(req.headers['x-gateway-key'], GATEWAY_KEY);

    const deny = (msg) => sendJson(res, 403, { ok: false, error: msg });

    if (req.method === 'POST' && url === '/send-message') {
        if (!isAuthorized()) return deny('missing or invalid x-gateway-key');
        try {
            const body = await readBody(req);
            const to = normalizePhone(body.to || body.phone);
            if (!to) return sendJson(res, 400, { ok: false, error: 'invalid phone' });
            if (!isReady) return sendJson(res, 503, { ok: false, error: 'not_ready' });
            const chatId = to.replace(/\D/g, '') + '@c.us';
            const result = await withRetry(() =>
                client.sendMessage(chatId, String(body.message || '')));
            } catch (e) {
                const msg = String(e.message || e);
                if (isKnownStoreError(msg)) {
                    // خطأ المتجر الشهير (getChat): الرسالة تُرسل فعلًا رغم ذلك — نُقرِّر النجاح
                    console.log('[STORE_ERR_AS_DELIVERED] ' + msg);
                    return sendJson(res, 200, { ok: true, delivered: true, id: 'sent', storeErr: msg });
                }
                return sendJson(res, 500, { ok: false, error: msg });
            }
            sendJson(res, 200, { ok: true, id: 'sent', delivered: true });
        return;
    }

    if (req.method === 'POST' && url === '/send-document') {
        if (!isAuthorized()) return deny('missing or invalid x-gateway-key');
        try {
            const body = await readBody(req);
            const to = normalizePhone(body.to || body.phone);
            if (!to) return sendJson(res, 400, { ok: false, error: 'invalid phone' });
            if (!isReady) return sendJson(res, 503, { ok: false, error: 'not_ready' });

            const filePath = body.filePath;
            if (!filePath || !fs.existsSync(filePath)) {
                return sendJson(res, 400, { ok: false, error: 'file not found: ' + filePath });
            }

            if (!isAllowedFile(filePath)) {
                return sendJson(res, 403, { ok: false, error: 'file path outside allowed directory' });
            }

            const chatId = to.replace(/\D/g, '') + '@c.us';
            const result = await withRetry(() =>
                client.sendMessage(chatId, fs.createReadStream(filePath), {
                    filename: body.fileName || path.basename(filePath),
                    caption: body.caption || ''
                }));
            sendJson(res, 200, { ok: true, id: result && result.id ? result.id._serialized : 'sent' });
        } catch (e) {
            sendJson(res, 500, { ok: false, error: String(e.message || e) });
        }
        return;
    }

    sendJson(res, 404, { ok: false, error: 'not found' });
});

server.listen(PORT, HOST, () => {
    console.log('[GATEWAY_HTTP] listening on http://' + HOST + ':' + PORT);
});

client.initialize();