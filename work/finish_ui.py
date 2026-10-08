from pathlib import Path
root = Path('outputs/TrafficPulseMvc')
css = root / 'TrafficWeb/wwwroot/css/site.css'
source = css.read_text(encoding='utf-8')
# Expand the existing stylesheet so students can edit one property at a time.
if source.count('\n') < 10:
    indent = 0
    lines = []
    current = ''
    for char in source.strip():
        if char == '{':
            lines.append('    ' * indent + current.strip() + ' {')
            current = ''
            indent += 1
        elif char == ';':
            lines.append('    ' * indent + current.strip() + ';')
            current = ''
        elif char == '}':
            if current.strip():
                lines.append('    ' * indent + current.strip() + ';')
            current = ''
            indent -= 1
            lines.append('    ' * indent + '}\n')
        else:
            current += char
    source = '\n'.join(lines)
source += '''
/* Dashboard map: a server-rendered image, with a clear disconnected state. */
.map-picker {
    display: flex;
    align-items: center;
    gap: 12px;
    margin: 18px 0;
}
.map-picker label { margin: 0; white-space: nowrap; }
.map-picker select { max-width: 330px; }
.map-picker button { white-space: nowrap; }
.map-caption { font-size: 13px; color: var(--muted); }
.traffic-map { display: block; width: 100%; height: auto; border-radius: 10px; }
.map-empty {
    min-height: 220px;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    text-align: center;
    padding: 28px;
    border: 1px dashed #b6cdd3;
    border-radius: 10px;
    background: #edf5f5;
}
.map-empty p { max-width: 480px; color: var(--muted); }
.map-symbol { color: var(--teal); font-size: 46px; line-height: 1; }
.stat { border-top: 3px solid var(--teal); }
.stat:last-child { border-top-color: #d49b28; }
.panel, .stat { box-shadow: 0 3px 16px #112f4005; }
tbody tr:hover { background: #f7fbfb; }
.sidebar a[aria-current=page] { background: #245064; border-right: 3px solid #7ee2cf; }
.auth-panel { border-top: 4px solid var(--teal); }
@media (max-width: 700px) {
    .map-picker { flex-wrap: wrap; }
    .map-picker select { max-width: none; }
    .map-caption { display: none; }
    .map-empty { min-height: 200px; padding: 20px; }
    .section-heading { flex-wrap: wrap; }
    .welcome p { font-size: 16px; }
}
'''
css.write_text(source, encoding='utf-8')
