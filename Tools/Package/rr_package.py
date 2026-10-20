"""Package the private campaign alpha (campaign alpha Prompt 20). Run from the repository root after a successful build:

    python Tools/Package/rr_package.py

Writes Builds/Release/RuleAndRuin_CampaignAlpha_<revision>.zip with the player (Builds/VS), the tester documents and a version
manifest. Leaves out Unity's debug-symbol folder ("..._BackUpThisFolder_ButDontShipItWithYourGame"), logs, saves, metrics and
anything else local. Nothing is uploaded; distribution is the creator's decision.
"""
import os, subprocess, sys, zipfile, json, datetime

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BUILD = os.path.join(ROOT, 'Builds', 'VS')
DOCS = [
    ('Docs/campaign-alpha/PLAY_README.md', 'README.md'),
    ('Docs/campaign-alpha/KNOWN_ISSUES.md', 'KNOWN_ISSUES.md'),
    ('Docs/campaign-alpha/RULES_SUMMARY.md', 'RULES_SUMMARY.md'),
    ('Docs/campaign-alpha/PLAYTEST_KIT.md', 'PLAYTEST_KIT.md'),
    ('Docs/vertical-slice/PLAYTEST_PROTOCOL.md', 'PLAYTEST_PROTOCOL.md'),
    ('Docs/campaign-alpha/BACKLOG.md', 'BACKLOG.md'),
    ('Docs/campaign-alpha/manifest/ship_manifest.md', 'ship_manifest.md'),
]
SKIP_DIRS = ('_BackUpThisFolder_ButDontShipItWithYourGame', 'Logs', 'saves')
SKIP_FILES = ('.jsonl', '.log')   # metrics and logs; the player's own .json files (ScriptingAssemblies, RuntimeInitializeOnLoads) are required
NEEDED_JSON = ('ScriptingAssemblies.json', 'RuntimeInitializeOnLoads.json')


def git(*args):
    return subprocess.check_output(['git', '-c', 'safe.directory=*'] + list(args), cwd=ROOT, text=True).strip()


def built_at():
    # The player's .exe stub is not rewritten by incremental builds; the game's own assemblies are.
    managed = os.path.join(BUILD, 'RuleAndRuin_Data', 'Managed')
    times = [os.path.getmtime(os.path.join(managed, f)) for f in os.listdir(managed) if f.startswith('RR.') and f.endswith('.dll')] if os.path.isdir(managed) else []
    t = max(times) if times else os.path.getmtime(os.path.join(BUILD, 'RuleAndRuin.exe'))
    return datetime.datetime.fromtimestamp(t).isoformat(timespec='seconds')


def main():
    if not os.path.exists(os.path.join(BUILD, 'RuleAndRuin.exe')): sys.exit('No build at Builds/VS; build first.')
    rev = git('rev-parse', '--short', 'HEAD')
    dirty = git('status', '--porcelain', '--', 'Assets', 'Tools/SliceTests/src', 'Packages')
    out_dir = os.path.join(ROOT, 'Builds', 'Release'); os.makedirs(out_dir, exist_ok=True)
    name = f'RuleAndRuin_CampaignAlpha_{rev}' + ('_modified' if dirty else '')
    path = os.path.join(out_dir, name + '.zip')
    manifest = {
        'package': name, 'revision': rev, 'branch': git('rev-parse', '--abbrev-ref', 'HEAD'),
        'source_modified_since_commit': bool(dirty), 'built': built_at(),
        'packaged': datetime.datetime.now().isoformat(timespec='seconds'),
        'rules_and_content': open(os.path.join(ROOT, 'Docs/campaign-alpha/manifest/ship_manifest.md'), encoding='utf-8').read().splitlines()[2],
        'private': 'Private campaign alpha for invited testers only. Not for redistribution or public release.',
    }
    count = 0
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        for base, dirs, files in os.walk(BUILD):
            dirs[:] = [d for d in dirs if not any(s in d for s in SKIP_DIRS)]
            for f in files:
                if f.endswith(SKIP_FILES) or (f.endswith('.json') and f not in NEEDED_JSON): continue   # no saves
                full = os.path.join(base, f)
                z.write(full, os.path.join(name, 'game', os.path.relpath(full, BUILD))); count += 1
        for src, dst in DOCS:
            full = os.path.join(ROOT, src)
            if os.path.exists(full): z.write(full, os.path.join(name, dst)); count += 1
            else: print('missing document', src)
        z.writestr(os.path.join(name, 'VERSION.json'), json.dumps(manifest, indent=2))
    print(f'{path}: {count} files, {os.path.getsize(path) // (1024 * 1024)} MB; revision {rev}' + (' (source modified since the commit)' if dirty else ''))


if __name__ == '__main__':
    main()
