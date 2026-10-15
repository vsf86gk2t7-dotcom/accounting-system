const qrcode = require('qrcode-terminal');
const fs = require('fs');
const raw = fs.readFileSync('C:/Users/my/AppData/Local/Temp/opencode/gw-out.log', 'utf8');
const m = raw.match(/\[QR_SCAN_NEEDED\]\s*([^\s]+)/);
if (!m) { console.log('NO_QR_IN_LOG'); process.exit(1); }
qrcode.generate(m[1], { small: true });