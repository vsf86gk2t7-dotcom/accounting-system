const http = require('http');
const fs = require('fs');
const path = require('path');

// 1) بناء ملف PDF اختباري صالح من الشفرة مباشرة (بدون مكتبات خارجية)
const pdfPath = path.join(__dirname, 'test-invoice-send.pdf');
const pdfBytes = buildTestPdf('Invoice #TEST-1001', 'Accounting System', 'Total: 1,500 EGP');
fs.writeFileSync(pdfPath, pdfBytes);
console.log('[PDF_WRITTEN] ' + pdfPath + ' bytes=' + pdfBytes.length);

// 2) التحقق من جاهزية البوابة
http.get('http://127.0.0.1:3210/status', (res) => {
    let data = '';
    res.on('data', (c) => (data += c));
    res.on('end', () => {
        console.log('[STATUS] ' + data);
        const st = JSON.parse(data);
        if (!st.ready) {
            console.log('[ABORT] gateway not ready');
            process.exit(1);
        }
        // 3) إرسال المستند
        const payload = JSON.stringify({
            to: '01116957655',
            filePath: pdfPath,
            fileName: 'invoice-TEST-1001.pdf',
            caption: 'فاتورة اختبار من النظام — مستند PDF'
        });
        const req = http.request({
            hostname: '127.0.0.1', port: 3210, path: '/send-document',
            method: 'POST',
            headers: {
                'Content-Type': 'application/json; charset=utf-8',
                'Content-Length': Buffer.byteLength(payload)
            }
        }, (resp) => {
            let body = '';
            resp.on('data', (c) => (body += c));
            resp.on('end', () => {
                console.log('[SEND_RESP_HTTP] ' + resp.statusCode);
                console.log('[SEND_RESP_BODY] ' + body);
                process.exit(0);
            });
        });
        req.on('error', (e) => { console.log('[SEND_ERR] ' + e.message); process.exit(1); });
        req.end(payload);
    });
}).on('error', (e) => { console.log('[CONN_ERR] ' + e.message); process.exit(1); });

function buildTestPdf(title, subtitle, total) {
    // بناء PDF يدوي بسيط صالح مع نص ASCII (لضمان العرض مهما كانت الشاشة)
    const line = (s) => utf8toHex(s) + '\n'; // placeholder
    function utf8toHex(s) {
        let h = '';
        for (let i = 0; i < s.length; i++) h += s.charCodeAt(i).toString(16).padStart(4, '0');
        return h;
    }
    return pdfTitle(title);
}

function pdfTitle(t) {
    const objects = [];
    // catalog + pages
    objects[1] = '<< /Type /Catalog /Pages 2 0 R >>';
    objects[2] = '<< /Type /Pages /Kids [3 0 R] /Count 1 >>';
    objects[3] = '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>';
    objects[4] = '<< /Length 0 >>\nstream\nBT /F1 18 Tf 72 780 Td (' + t + ') Tj ET\nendstream';
    objects[5] = '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>';

    let out = '%PDF-1.4\n';
    const offsets = [];
    for (let i = 1; i <= 5; i++) {
        offsets[i] = out.length;
        out += i + ' 0 obj\n' + objects[i] + '\nendobj\n';
    }
    const xrefPos = out.length;
    out += 'xref\n0 ' + 6 + '\n0000000000 65535 f \n';
    for (let i = 1; i <= 5; i++) {
        out += String(offsets[i]).padStart(10, '0') + ' 00000 n \n';
    }
    out += 'trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n' + xrefPos + '\n%%EOF\n';
    return Buffer.from(out, 'latin1');
}