# 🛡️ Dynamic IP Change Monitor & Notifier

A lightweight, zero-overhead background service built with **.NET 10** and **SQLite** designed to detect public IP address changes and instantly notify a list of email recipients via SMTP.

---

## 💡 The Problem

Running a self-hosted OpenVPN server behind a residential or dynamic IP connection usually forces you to either:
1. Use public Dynamic DNS (DDNS) providers, making your home IP easily discoverable.
2. Keep the OpenVPN port permanently open to the public internet in your router's DMZ, exposing it to automated `nmap` port scans, botnets, and continuous brute-force attacks.

## 🎯 The Solution

Instead of broadcasting your IP via DDNS or keeping a statically mapped entry active, this service monitors your public WAN IP at set intervals. When your ISP changes your IP address:
* The new IP is saved to an embedded, volume-persisted **SQLite** database.
* An immediate email alert is dispatched to your configured administrator list.
* You update your DMZ/firewall rules or target endpoint manually only when needed, keeping your infrastructure hidden from blind internet scans.

---

## ✨ Features

* **Zero-Configuration SQLite Persistence**: Survives container restarts and host reboots. If your IP changes while the service is offline, it detects the shift immediately upon booting and triggers an alert.
* **Redundant IP Resolution**: Cycles through multiple reliable public IP endpoints (`ipify`, `icanhazip`, `ipinfo`) to eliminate false alerts caused by single-provider downtime.
* **Multi-Recipient Email Support**: Send formatted alerts to one or multiple comma-separated email addresses.
* **Resource Efficient**: Runs as an optimized, lightweight container consuming minimal CPU and RAM.

---

### 🛠️ Architecture Overview

```
[Public IP APIs] <--- (Polls every X min)
         |
  [IP Monitor Service]
         |
         +---> [SQLite DB (/data/ip_history.db)]  (Checks & Logs change)
         |
         +---> [SMTP Server] ---> [Email Alert Sent to Admins]
```

---

## 🚀 Quick Start (Docker Compose)

The easiest way to run the application is via `docker-compose`.

### 1. Environment Variables
  
| Variable | Description | Default / Options |
| --- | --- | --- |
| `CHECK_INTERVAL_MINUTES` | Frequency (in minutes) to poll for IP changes. | `10` |
| `EMAIL_DELIVERY_MODE` | Delivery strategy to use. | `Smtp` or `DirectMx` |
| `SMTP_HOST` | Hostname of your SMTP provider (Required if mode is `Smtp`). | `smtp.gmail.com` |
| `SMTP_PORT` | Port for your SMTP server. | `587` |
| `SMTP_USER` | SMTP authentication username (Required if mode is `Smtp`). | *Optional in DirectMx* |
| `SMTP_PASS` | SMTP authentication password (Required if mode is `Smtp`). | *Optional in DirectMx* |
| `EMAIL_FROM` | Sender email address displayed in the alert. | *Required* |
| `EMAIL_TO` | Comma-separated list of recipient email addresses. | *Required* |


### 2. `docker-compose.yml`

```yaml

services:
  ip-monitor:
    build: .
    container_name: ip_monitor
    restart: unless-stopped
    volumes:
      - ip_data:/data
    environment:
      # Intervalo de checagem em minutos
      - CHECK_INTERVAL_MINUTES=10

      # Modo de entrega: 'Smtp' (padrão) ou 'DirectMx'
      - EMAIL_DELIVERY_MODE=Smtp

      # Obrigatório se EMAIL_DELIVERY_MODE=Smtp
      - SMTP_HOST=smtp.gmail.com
      - SMTP_PORT=587
      - SMTP_USER=seu-email@gmail.com
      - SMTP_PASS=sua-senha-de-app

      # Obrigatório em todos os modos
      - EMAIL_FROM=seu-email@gmail.com
      - EMAIL_TO=destino1@email.com,destino2@email.com

volumes:
  ip_data:

```

### 3. Run the container

```
docker compose up -d
```


## 📄 License

This project is open-source and available under the MIT License.
