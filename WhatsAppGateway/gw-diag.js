const { Client, LocalAuth } = require("whatsapp-web.js");
const client = new Client({ authStrategy: new LocalAuth({ dataPath: require("path").join(__dirname, ".wapp-session") }), puppeteer: { headless: true, args: ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage", "--disable-gpu"] } });
client.on("qr", (qr) => console.log("[QR_SCAN_NEEDED] " + qr));
client.on("ready", async () => {
  console.log("[GATEWAY_READY] id=" + client.info.wid.user);
  try {
    const ok = await client.sendMessage("201116957655@c.us", "اختبار تشخيصي من نظام المحاسبة ✔");
    console.log("[SEND_RESULT] " + JSON.stringify(ok));
  } catch (e) {
    console.log("[SEND_ERR] " + (e && e.name) + ": " + (e && e.message));
    if (e && e.stack) console.log(e.stack);
  }
  process.exit(0);
});
client.on("auth_failure", (m) => console.log("[AUTH_FAILURE] " + m));
client.initialize();
