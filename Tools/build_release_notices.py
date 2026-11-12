"""Refresh bundled notices from the project's installed, pinned sources.

Run after changing runtime packages or third-party art. Does not certify purchases.
"""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'Assets/StreamingAssets/Notices'
REPORT = ROOT / 'Logs/SteamRelease/SR-12'


def build():
    DEST.mkdir(parents=True, exist_ok=True)
    REPORT.mkdir(parents=True, exist_ok=True)
    sources = {
        'Galmuri-OFL.txt': 'Assets/Resources/UI/Fonts/Galmuri-OFL.txt',
        'LiberationSans-OFL.txt': 'Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt',
    }
    copied = []
    for name, source in sources.items():
        src, dst = ROOT / source, DEST / name
        shutil.copyfile(src, dst)
        assert dst.read_bytes() == src.read_bytes()
        copied.append(dict(source=source, output=name, sha256=hashlib.sha256(dst.read_bytes()).hexdigest()))

    icons = (ROOT / 'Assets/Art/UI/CombatStatusIcons/ATTRIBUTION.md').read_text(encoding='utf-8-sig')
    # Keep individual authors/links and the modification notice; omit developer-only instructions.
    icons = icons.split('Source SVGs are retained')[0].rstrip() + '\n'
    import re
    icons = re.sub(r'New sources are pinned.*?artwork\.\n', '', icons, flags=re.S)
    icons = icons.removeprefix('# ')
    icons = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'\1 — \2', icons)
    (DEST / 'GameIcons.txt').write_text(icons, encoding='utf-8')
    audio = (ROOT / 'Assets/Resources/Audio/Combat/THIRD_PARTY_AUDIO.md').read_text(encoding='utf-8-sig')
    audio = audio.replace('# Combat audio sources', 'Legacy combat sample library')
    audio = audio.replace('`Samples/`', 'Audio/Combat/Samples')
    (DEST / 'AudioSources.txt').write_text(audio, encoding='utf-8')
    other = ('Additional assets\n\nGenerated sound effects\nElevenLabs — elevenlabs.io\n\n'
             'Visual effects\nVefects — Zap VFX - URP\n'
             'https://assetstore.unity.com/packages/vfx/particles/spells/zap-vfx-urp-303479\n\n'
             'Music\nMusic: Afternoon by Redsen Game Music\n'
             'https://assetstore.unity.com/packages/audio/music/freebgm-afternoon-391254\n\n'
             'Sound libraries\nleohpaz — RPG Essentials Sound Effects - FREE!\n'
             'https://assetstore.unity.com/packages/audio/sound-fx/rpg-essentials-sound-effects-free-227708\n\n'
             'Multiple Solutions — Basic RPG Sounds\n'
             'https://assetstore.unity.com/packages/audio/sound-fx/basic-rpg-sounds-325670\n\n'
             'AD Sounds / Alan Dalcastagne — Combat Magic Spells - Sound Effects\n'
             'https://assetstore.unity.com/packages/audio/sound-fx/combat-magic-spells-sound-effects-214178\n\n'
             'Daniel SoundsGood — Free Deadly Kombat\n'
             'https://assetstore.unity.com/packages/audio/sound-fx/free-deadly-kombat-228835\n\n'
             'Epic Sounds and FX — Epic Game Hits SFX\n'
             'https://assetstore.unity.com/packages/audio/sound-fx/epic-game-hits-sfx-36617\n\n'
             'PlaceHolder Inc — Casual Game Sounds - Indie Friendly Packs\n'
             'https://assetstore.unity.com/packages/audio/sound-fx/casual-game-sounds-indie-friendly-packs-283136\n\n'
             'Tim Beek — Free Casual SoundFX Pack (Tribal Drum)\n'
             'https://assetstore.unity.com/packages/audio/sound-fx/free-casual-soundfx-pack-164843\n\n'
             'Asset Store libraries are incorporated in the game under the Standard Unity Asset Store EULA.\n'
             'https://unity.com/legal/as-terms\n\n'
             'URP debug font\nPerfect DOS VGA 437 — Zeh Fernando\n'
             'Bundled in Unity Render Pipeline Core.\n'
             'Author statement (September 16, 2014):\n'
             '"The license is this: this font is free to use in whatever you want."\n'
             'https://www.dafont.com/font-comment.php?file=perfect_dos_vga_437\n')
    (DEST / 'OtherAssets.txt').write_text(other, encoding='utf-8')

    packages = [
        'com.unity.barracuda@3.0.0', 'com.unity.burst@1.8.21',
        'com.unity.mathematics@1.2.6', 'com.unity.ml-agents@2.3.0-exp.3',
        'com.unity.nuget.newtonsoft-json@3.2.1',
        'com.unity.render-pipelines.core@14.0.12',
        'com.unity.render-pipelines.universal@14.0.12',
        'com.unity.render-pipelines.universal-config@14.0.10',
        'com.unity.shadergraph@14.0.12', 'com.unity.textmeshpro@3.0.7',
        'com.unity.test-framework@1.1.33',
        'com.unity.timeline@1.7.7', 'com.unity.ugui@1.0.0', 'com.unity.visualscripting@1.9.4',
    ]
    software = ['Runtime package notices\n\nOriginal notices from the installed Unity packages.\n']
    for package in packages:
        folder = ROOT / 'Library/PackageCache' / package
        assert folder.is_dir(), folder
        paths = sorted(p for p in folder.rglob('*') if p.is_file()
                       and p.name in ('LICENSE', 'LICENSE.txt', 'LICENSE.md', 'Third Party Notices.md')
                       and 'Samples~' not in p.parts)
        assert paths, package
        for path in paths:
            content = path.read_text(encoding='utf-8-sig')
            software.append('\n\n' + '=' * 50 + '\n' + package + ' / ' + path.relative_to(folder).as_posix() + '\n\n' + content)
            copied.append(dict(source=path.relative_to(ROOT).as_posix(), output='Software.txt',
                               sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    mcp = ROOT / 'Tools/Notices/MCP-LICENSE.txt'
    software.append('\n\nMCP for Unity (11836003a5e2ffcb7715ecec7e1fbb9d9cdb5bb8)\n'
                    'https://github.com/CoplayDev/unity-mcp\n\n' + mcp.read_text(encoding='utf-8-sig'))
    copied.append(dict(source=mcp.relative_to(ROOT).as_posix(), output='Software.txt',
                       sha256=hashlib.sha256(mcp.read_bytes()).hexdigest()))
    (DEST / 'Software.txt').write_text(''.join(software), encoding='utf-8')
    (REPORT / 'notice-sources.json').write_text(json.dumps(copied, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'notices': [p.name for p in DEST.glob('*.txt')], 'source_count': len(copied)}))


if __name__ == '__main__':
    build()
