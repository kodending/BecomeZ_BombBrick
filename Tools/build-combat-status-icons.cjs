// Rasterize the approved Game-icons.net originals. Run with the existing sharp runtime.
const fs = require("node:fs");
const path = require("node:path");
const assert = require("node:assert/strict");
const sharp = require("sharp");
const project = path.resolve(__dirname, "..");
const source = path.join(project, "Assets/Art/UI/CombatStatusIcons/Source");
const output = path.join(project, "Assets/Resources/UI/CombatStatusIcons");
const catalog = require("../Assets/Art/UI/CombatStatusIcons/catalog.json");

async function build() {
    fs.mkdirSync(output, { recursive: true });
    const raster = name => sharp(path.join(source, name.replace(/^local\//, "")))
        .resize(64, 64).png();
    for (const [name, entry] of Object.entries(catalog)) {
        if (name !== "AttackSpeedDown")
            await raster(entry.source).toFile(path.join(output, name + ".png"));
    }
    // Same placement as the approved preview: 48% width, 58% height, bottom right.
    const hourglass = await sharp(path.join(source, "sands-of-time.svg"))
        .resize(31, 37, { fit: "contain", background: "#000000" }).png().toBuffer();
    const sword = await raster("local/broadsword.svg").toBuffer();
    await sharp(sword).composite([{ input: hourglass, left: 33, top: 27 }])
        .png().toFile(path.join(output, "AttackSpeedDown.png"));
    for (const name of Object.keys(catalog)) {
        const metadata = await sharp(path.join(output, name + ".png")).metadata();
        assert.equal(metadata.width, 64);
        assert.equal(metadata.height, 64);
        console.log(name + ": 64x64 PNG");
    }
    if (process.argv.includes("--contact-sheet")) {
        const cells = [];
        const names = Object.keys(catalog);
        for (let i = 0; i < names.length; i++) {
            const x = (i % 8) * 140, y = Math.floor(i / 8) * 112;
            const file = path.join(output, names[i] + ".png");
            cells.push({ input: await sharp(file).toBuffer(), left: x + 22, top: y + 4 });
            cells.push({ input: await sharp(file).resize(16, 16).toBuffer(), left: x + 106, top: y + 29 });
            const label = Buffer.from('<svg width="140" height="32"><text x="70" y="19" text-anchor="middle" fill="white" font-family="sans-serif" font-size="11">' + names[i] + '</text></svg>');
            cells.push({ input: label, left: x, top: y + 72 });
        }
        await sharp({ create: { width: 1120, height: Math.ceil(names.length / 8) * 112,
            channels: 4, background: "#182028" } }).composite(cells).png()
            .toFile(path.join(project, "Logs/FangLockVerification/all-status-icons-contact-sheet.png"));
    }
}
build().catch(error => { console.error(error); process.exitCode = 1; });
