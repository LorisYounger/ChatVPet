"""Checks the running workbench without using any remote model or player save."""
import json
import base64
import pathlib
import time
import sys
from workbench import call

base = sys.argv[1] if len(sys.argv) > 1 else 'http://127.0.0.1:39421'
checks = []


def check(name, passed):
    checks.append({'name': name, 'passed': bool(passed)})
    if not passed: raise AssertionError(name)


def api(path, body=None):
    return call(base, path, body)


def wait(identifier):
    deadline = time.monotonic() + 45
    while time.monotonic() < deadline:
        state = api('sessions/' + identifier)
        if not state['busy']: return state
        time.sleep(.15)
    raise TimeoutError(identifier)


info = api('info')
check('test suites available', 'compression' in info['suites'])
identifier = api('sessions', {'Title': '自动验收 · 离线与实际插件', 'Offline': True, 'Rag': False})['id']
prefix = 'sessions/' + identifier
for check_result in api(prefix + '/plugin-check', {}):
    check('plugin: ' + check_result['name'], check_result['passed'])
api(prefix + '/steps', {'steps': [{'Text': '手铃叫浅滩。'}, {'Text': '画架叫江灯。', 'Reload': True}]})
state = wait(identifier)
check('offline turns complete', state['status'] == '已完成' and len(state['checkpoint']['dialogues']) == 2)
wire = state['checkpoint']['messages']
check('export preserves the two accepted messages per plain turn',
      [m['source'] for m in wire] == ['玩家', 'ChatVPet'] * 2
      and wire[0]['turnId'] == wire[1]['turnId'] != wire[2]['turnId']
      and wire[0]['content'] == state['checkpoint']['dialogues'][0]['Turn']['InitialInput'])
check('process result visible', any(e['kind'] == 'turn' for e in state['events']))
check('no remote calls in offline mode', not any(e['kind'] in ('chat', 'tool', 'summary', 'embedding', 'rerank', 'vision') for e in state['events']))
check('checkpoint reload recorded', any(e['kind'] == 'checkpoint' for e in state['events']))
check('actual prompt captured', any(e['kind'] == 'offline' and 'system' in e['data'] for e in state['events']))
check('API keys redacted', all(v == '(不导出)' for k, v in state['configuration'].items() if 'key' in k.lower()))
fixture = pathlib.Path(__file__).resolve().parents[1] / 'ChatVPet.ChatProcess.ConsoleTest/Fixtures/vision.png'
api(prefix + '/steps', {'Text': '离线图片检查', 'ImageBase64': base64.b64encode(fixture.read_bytes()).decode()})
state = wait(identifier)
check('offline image stays offline', any(e['kind'] == 'observation' for e in state['events']) and not any(e['kind'] == 'vision' for e in state['events']))
seq = state['events'][-1]['seq']
api(prefix + '/notes', {'text': '已核对实际插件购买逻辑。'})
delta = api(prefix + '/events?after=' + str(seq))
check('incremental event cursor', len(delta['events']) == 1 and delta['events'][0]['kind'] == 'note')
branches = api(prefix + '/compare', {'text': '手铃叫什么？', 'variants': ['baseline', 'no-rag', 'no-summary']})['ids']
for branch in branches:
    child = wait(branch)
    check('branch independent: ' + branch, len(child['checkpoint']['dialogues']) == 4 and child['status'] == '已完成')
check('source unchanged after branches', len(api(prefix)['checkpoint']['dialogues']) == 3)
check('comparison group shared', len({next(e['data']['group'] for e in api('sessions/' + i)['events'] if e['kind'] == 'comparison') for i in branches}) == 1)
api(prefix + '/steps', {'steps': [{'Text': '停止检查 ' + str(i)} for i in range(100)]})
api(prefix + '/stop', {})
state = wait(identifier)
check('stop finishes current step and ends batch', state['status'] == '已停止' and len(state['checkpoint']['dialogues']) < 103)
print(json.dumps({'id': identifier, 'passed': len(checks), 'checks': checks}, ensure_ascii=True, indent=2))
