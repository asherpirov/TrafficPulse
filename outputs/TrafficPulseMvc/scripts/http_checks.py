"""Smoke checks for a LOCAL DEMO server; creates one disposable demo account."""
import http.cookiejar
import json
import sys
import uuid
from html import unescape
from html.parser import HTMLParser
from urllib.error import HTTPError
from urllib.parse import urlencode, urlparse
from urllib.request import build_opener, HTTPCookieProcessor

base = (sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:5187').rstrip('/')
assert urlparse(base).hostname in ('localhost', '127.0.0.1'), 'Local demo only'

class Inputs(HTMLParser):
    def __init__(self, html):
        super().__init__()
        self.values = {}
        self.feed(html)

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if tag == 'input' and 'name' in values:
            self.values[values['name']] = values.get('value', '')

class Client:
    def __init__(self):
        self.http = build_opener(HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(self, path, data=None):
        encoded = None if data is None else urlencode(data).encode()
        try:
            response = self.http.open(base + path, encoded, timeout=30)
        except HTTPError as error:
            response = error
        return response.status, response.read().decode(), response.url

    def post(self, page, path, values):
        status, html, _ = self.request(page)
        assert status == 200
        values = dict(values, __RequestVerificationToken=Inputs(html).values['__RequestVerificationToken'])
        return self.request(path, values)

    def login(self, email):
        result = self.post('/Account/Login', '/Account/Login', {'Email': email, 'Password': 'TrafficDemo2026!'})
        assert '/Traffic' in result[2] or result[2].rstrip('/') == base

def check(condition, name):
    assert condition, name
    print('PASS:', name)

anonymous = Client()
check(anonymous.request('/api/traffic/roads')[0] == 401, 'Anonymous API rejected')
check('demo-banner' in anonymous.request('/Account/Login')[1], 'Demo mode confirmed')
user = Client()
email = 'http-' + uuid.uuid4().hex[:10] + '@example.test'
result = user.post('/Account/Register', '/Account/Register', {
    'Name': 'HTTP Test', 'Email': email, 'Password': 'TrafficDemo2026!',
    'ConfirmPassword': 'TrafficDemo2026!', 'Role': 'Admin'})
check('/Account/Login' in result[2], 'Registration succeeds')
user.login(email)
check(user.request('/Admin/Users')[0] == 403, 'Injected admin role ignored; admin URL forbidden')
roads = json.loads(user.request('/api/traffic/roads')[1])
road = roads[0]['id']
check(user.request('/Traffic/Favorite', {'id': road, 'enabled': 'true'})[0] == 400, 'CSRF required')
user.post('/Traffic', '/Traffic/Favorite', {'id': road, 'enabled': 'true'})
check(roads[0]['name'] in unescape(user.request('/Traffic?favoritesOnly=true')[1]), 'Favorite displayed')
user.post('/Traffic', '/Traffic/Favorite', {'id': road, 'enabled': 'false'})
check(user.request('/Traffic/History/' + road)[0] == 200, 'History renders')
check(user.request('/Traffic/History/' + str(uuid.uuid4()))[0] == 404, 'Unknown road rejected')
check(user.request('/Alerts')[0] == 200, 'Alerts render')
admin = Client()
admin.login('admin@traffic.demo')
check(admin.request('/Admin/Roads')[0] == 200, 'Admin roads render')
status, html, _ = admin.request('/Admin/Users')
check(status == 200 and email in html, 'Admin sees new user')
# The user row contains its own form and identifier.
row = next(row for row in html.split('<tr>') if email in row)
user_id = Inputs(row).values['id']
admin.post('/Admin/Users', '/Admin/SetUserActive', {'id': user_id, 'active': 'false'})
check(user.request('/api/traffic/roads')[0] == 401, 'Disabled user session revoked')
check(admin.request('/Account/Logout')[0] == 405, 'Logout requires POST')
print('HTTP checks complete; test account disabled:', email)
