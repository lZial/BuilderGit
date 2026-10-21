# Удалённый доступ к OpenCode через Tailscale

Настроено: 2026-10-20. Вариант A2 (приватная сеть, ничего не светится в интернет).

## Как пользоваться (телефон)

1. Установить **Tailscale** на телефон и войти тем же аккаунтом (`lZial@`).
2. Открыть в браузере: `http://100.125.165.117:45278`
3. Войти паролем сервера: `Y_j8co0CRiGTt-ymaHQwfS3m4cVzTsOwb5wVXO8bHFc`
   (или одноразовая ссылка: см. ниже)
4. Добавить страницу в «Избранное» / на домашний экран.

Получить одноразовую ссылку (действует 5 минут, один раз):

```powershell
& "$env:APPDATA\ai.opencode.desktop\cli\2.0.32-x64\opencode-cli.exe" pair
```

В выводе будет строка с адресом `http://100.125.165.117:45278/auth/connect/КОД` — её и открыть на телефоне.
Сессия браузера после этого живёт 30 дней.

## Что настроено на ПК

| Что | Значение |
|---|---|
| Слушающий адрес сервера | `0.0.0.0:45278` (`opencode service set hostname 0.0.0.0`, `port 45278`) |
| Tailscale IP | `100.125.165.117` |
| Служба Tailscale | `Automatic` (стартует с системой) |
| Автозапуск сервера OpenCode | `OpenCode Service.cmd` в папке `Автозагрузка` (путь к CLI ищется динамически) |
| Правила брандмауэра | `OpenCode via Tailscale` — Allow, TCP 45278, remote `100.64.0.0/10`<br>`OpenCode via Tailscale (any port)` — Allow, программа opencode-cli.exe, remote `100.64.0.0/10`<br>`OpenCode block LAN` — Block, TCP 45278, remote `192.168.0.0/24` |
| Широкие правила `opencode-cli.exe` (Allow, любой порт/любой IP) | **отключены** |

## Изоляция (проверено)

- Маршрут по умолчанию не изменился: Wi-Fi `192.168.0.1`, metric 0 → трафик к LLM-провайдерам не идёт через туннель.
- `netsh winhttp show proxy` и `HKCU\...\Internet Settings` — без изменений, системного прокси нет.
- Локальный доступ `http://127.0.0.1:45278` (десктоп-приложение) работает как раньше.
- В интернет сервер не выставлен: ни проброса портов, ни `pair --remote`, ни публичного туннеля.

## Откат

```powershell
& "$env:APPDATA\ai.opencode.desktop\cli\2.0.32-x64\opencode-cli.exe" service set hostname 127.0.0.1
& "$env:APPDATA\ai.opencode.desktop\cli\2.0.32-x64\opencode-cli.exe" service start
# правила брандмауэра удаляются elevated:
# Remove-NetFirewallRule -DisplayName "OpenCode via Tailscale"
# Remove-NetFirewallRule -DisplayName "OpenCode via Tailscale (any port)"
# Remove-NetFirewallRule -DisplayName "OpenCode block LAN"
```

## Проверено 2026-10-20

- Телефон `POCO X5 5G` в tailnet: `100.106.177.17`, MagicDNS `poco-x5-5g.tail97d5da.ts.net`.
- Канал до телефона: relay `ams` (Amsterdam), ~162 мс, прямой P2P не устанавливается (UDP режется сетью оператора) — на работу не влияет.
- Веб-UI на телефоне: `http://100.125.165.117:45278` → вход по адресу + паролю (`opencode` / пароль сервера) либо по одноразовой ссылке `opencode pair`.
- Файл конфигурации сервера с паролем: `C:\Users\shist\.config\opencode\service.json`
  (`hostname`, `port`, `password`) — там же меняется пароль вручную.

## Что стоит помнить

- ПК должен быть включён: Tailscale не будит его из сна, связь теряется при переходе в сон.
- На Android отключите оптимизацию батареи для Tailscale (`Настройки → Приложения → Tailscale → Батарея → Без ограничений`), иначе Android может убить VPN-сервис.
- Сессия браузера на телефоне живёт 30 дней; смена пароля сервера обнуляет все сессии.

## Публичный доступ через Tailscale Funnel (основной способ с телефона)

Схема: телефон со своим VPN (единственный VPN-профиль) → обычный HTTPS → Funnel → ПК → OpenCode.
Tailscale на телефоне при этом **не нужен**, поэтому конфликт двух VPN исключён.

- Адрес: `https://laptop-6vcur215.tail97d5da.ts.net` (валидный TLS-сертификат, без предупреждений браузера).
- Прокси: `tailscale funnel --bg 45278` → `http://127.0.0.1:45278`.
- Включено в админке: `https://login.tailscale.com/f/serve?node=nD6sjfqqTw11CNTRL` (Serve + Funnel).
- Статус: `tailscale funnel status`; выключить: `tailscale funnel --https=443 off`.
- Вход: логин `opencode`, пароль сервера. Либо одноразовая ссылка (cookie-сессия на 30 дней):
  ```powershell
  & "$env:APPDATA\ai.opencode.desktop\cli\2.0.32-x64\opencode-cli.exe" pair
  ```
  В выводе взять код и открыть на телефоне
  `https://laptop-6vcur215.tail97d5da.ts.net/auth/connect/<КОД>`.
  **Код одноразовый и живёт 5 минут** — не проверяйте его curl'ом, он сгорает.

⚠️ Сервер открыт в интернет по этому адресу — защита только паролем. При желании сменить:
```powershell
& "$env:APPDATA\ai.opencode.desktop\cli\2.0.32-x64\opencode-cli.exe" service set password "новый-пароль"
& "$env:APPDATA\ai.opencode.desktop\cli\2.0.32-x64\opencode-cli.exe" service start
```

## Exit node: запасной вариант (без публичного доступа)

Проблема, которую он решает: Android держит только один активный VPN-сервис, поэтому Tailscale и VPN-клиент телефона вытесняли друг друга.
Решение: ПК объявлен exit node — телефон запускает только Tailscale, а весь его интернет-трафик идёт по туннелю на ПК и выходит оттуда через v2RayTun (режим ТУННЕЛЬ, узел Estonia VLESS/GRPC).

На ПК настроено:
- `netsh interface ipv4 set forwardenable=1`
- `Set-NetIPInterface -InterfaceAlias "Tailscale" / "<Wi-Fi>" -Forwarding Enabled`
- `tailscale set --advertise-exit-node=true` → `AdvertiseRoutes: 0.0.0.0/0, ::/0`
- правило брандмауэра `Tailscale transit (exit node)`: Inbound / Allow / Any / remote `100.64.0.0/10` / interface Tailscale

На телефоне: Tailscale → «Use exit node» → `laptop-6vcur215`; VPN-клиент телефона при этом выключен.
Проверка: `https://ifconfig.me` на телефоне должен показать Cloudflare-IP (`104.28.201.138`).

Откат exit node:

```powershell
& "C:\Program Files\Tailscale\tailscale.exe" set --advertise-exit-node=false
```

Внешний IP ПК после включения v2RayTun: `104.28.201.138` — это IP узла Estonia (VLESS/GRPC поверх Cloudflare CDN), а не WARP.
Cloudflare WARP на ПК отключён: служба `CloudflareWARP` → Status `Stopped`, StartType `Manual` (GUI-процесс тоже закрыт). Вернуть: `Set-Service CloudflareWARP -StartupType Automatic; Start-Service CloudflareWARP` (elevated) или запустить «Cloudflare WARP» из меню «Пуск».
