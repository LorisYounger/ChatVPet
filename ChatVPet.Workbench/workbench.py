"""ChatVPet Lab automation client. Uses the same service as the WPF window."""
import argparse
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path


def call(base, path, body=None):
    request = urllib.request.Request(
        base.rstrip('/') + '/api/' + path,
        data=None if body is None else json.dumps(body, ensure_ascii=False).encode('utf-8'),
        headers={'Content-Type': 'application/json'},
    )
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        raise RuntimeError(error.read().decode('utf-8')) from error


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', default='http://127.0.0.1:39421')
    sub = parser.add_subparsers(dest='command', required=True)
    sub.add_parser('info')
    sub.add_parser('list')
    new = sub.add_parser('new')
    new.add_argument('--title', default='自动化测试')
    new.add_argument('--config', default='ChatVPetProcess.Qw35-v1.Settings.json')
    new.add_argument('--compression', choices=['light', 'heavy', 'off'], default='light')
    new.add_argument('--offline', action='store_true')
    new.add_argument('--no-rag', action='store_true')
    new.add_argument('--no-rerank', action='store_true')
    send = sub.add_parser('send')
    send.add_argument('id')
    send.add_argument('text')
    send.add_argument('--expected', default='')
    send.add_argument('--image')
    batch = sub.add_parser('batch')
    batch.add_argument('id')
    batch.add_argument('file', help='UTF-8 JSON array of TestStep objects')
    suite = sub.add_parser('suite')
    suite.add_argument('name', choices=['baseline', 'compression'])
    suite.add_argument('--config', default='ChatVPetProcess.Qw35-v1.Settings.json')
    compare = sub.add_parser('compare')
    compare.add_argument('id')
    compare.add_argument('text')
    compare.add_argument('--variant', choices=['no-rag', 'no-rerank', 'no-summary', 'compatibility'], default='no-rag')
    for name in ['get', 'stop', 'retry', 'plugin-check']:
        sub.add_parser(name).add_argument('id')
    events = sub.add_parser('events'); events.add_argument('id'); events.add_argument('--after', type=int, default=0)
    wait = sub.add_parser('wait'); wait.add_argument('id'); wait.add_argument('--seconds', type=int, default=30)
    export = sub.add_parser('export'); export.add_argument('id'); export.add_argument('file')
    note = sub.add_parser('note'); note.add_argument('id'); note.add_argument('text')
    args = parser.parse_args()
    api = lambda path, body=None: call(args.url, path, body)
    command = args.command
    prefix = 'sessions/' + args.id if hasattr(args, 'id') else ''
    if command == 'info': result = api('info')
    elif command == 'list': result = api('sessions')
    elif command == 'new':
        result = api('sessions', {'Title': args.title, 'Config': args.config, 'Compression': args.compression,
                                 'Offline': args.offline, 'Rag': not args.no_rag, 'Rerank': not args.no_rerank})
    elif command == 'send':
        body = {'Text': args.text, 'Expected': args.expected}
        if args.image:
            import base64
            body['ImageBase64'] = base64.b64encode(Path(args.image).read_bytes()).decode()
        result = api(prefix + '/steps', body)
    elif command == 'batch': result = api(prefix + '/steps', {'steps': json.loads(Path(args.file).read_text(encoding='utf-8-sig'))})
    elif command == 'suite': result = api('suites', {'name': args.name, 'options': {'Config': args.config}})
    elif command == 'compare': result = api(prefix + '/compare', {'text': args.text, 'variants': ['baseline', args.variant]})
    elif command in ['stop', 'retry', 'plugin-check']: result = api(prefix + '/' + command, {})
    elif command == 'note': result = api(prefix + '/notes', {'text': args.text})
    elif command == 'get': result = api(prefix)
    elif command == 'export':
        Path(args.file).write_text(json.dumps(api(prefix + '/export'), ensure_ascii=False, indent=2), encoding='utf-8')
        result = {'saved': args.file}
    elif command == 'events': result = api(prefix + '/events?after=' + str(args.after))
    else:
        deadline = time.monotonic() + args.seconds
        while True:
            result = api(prefix + '/events?after=2147483647')
            if not result['busy'] or time.monotonic() >= deadline: break
            time.sleep(1)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    if hasattr(sys.stdout, 'reconfigure'): sys.stdout.reconfigure(encoding='utf-8')
    try: main()
    except (RuntimeError, OSError, ValueError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
