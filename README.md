<!-- Header con estilo personalizado -->
<div style="
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: linear-gradient(135deg, #9B68A7 0%, #7A4A7C 100%);
    border: 4px solid rgba(255, 255, 255, 0.1);
    border-radius: 25px;
    padding: 30px 50px;
    font-family: Arial, sans-serif;
    color: #ffffff;
    max-width: 100%;
    margin: 30px auto;
    box-shadow: 
        0 10px 20px rgba(0, 0, 0, 0.2),
        inset 0 2px 10px rgba(255, 255, 255, 0.1);
">
    <div style="flex-grow: 1;">
        <h1 style="
            font-size: 4.2em;
            margin: 10px 0;
            font-weight: 800;
            letter-spacing: -1px;
            text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.2);
        ">Watchdog Middleware <span style="font-size: 0.9em;">🐶</span></h1>
    </div>
    <div style="
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        margin-left: 40px;
    ">
        <p style="font-size: 1.3em; margin: 5px 0; font-weight: bold;">By Rubén Martínez</p>
    </div>
</div>

---

## 🧠 Overview

**Watchdog Middleware** is a lightweight, pluggable .NET middleware component designed to **intercept, monitor, and store API traffic** with minimal configuration.

It captures request and response details, stores the data in **InfluxDB**, and offers real-time visualization and monitoring through **Grafana**. It's ideal for projects that require **observability**, **error analysis**, and **performance tracking** without intrusive instrumentation.

Inspired by both the electronic watchdog timer and the loyal API guardian, this middleware ensures your APIs are always under control. 🛡️

---

## ⚙️ Features

- 📡 **Intercepts all API requests/responses**
- 🗃️ **Saves data in InfluxDB** using customizable fields and tags
- 📊 **Integrated with Grafana** for real-time dashboards
- 🔐 Optional **sensitive route exclusion** and **body encryption**
- 🐳 Fully **Docker-ready** stack for local or cloud deployments
- 📍 **IP Geolocation support** out of the box

---

## 🚀 Quick Start

### 🐳 1. Clone and Launch via Docker

```bash
git clone https://github.com/RubenMartinezSiso/WatchdogMiddleware.git
cd WatchdogMiddleware/WatchdogMiddleware
docker compose up -d
```

> Make sure Docker is installed and running.

---

### 🧱 2. Add Middleware to Your ASP.NET Core App

```csharp
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.UseWatchdogMiddleware(options =>
    {
        options.ApiName = "MyAwesomeAPI";
        options.ActivateLogs = true;
        // Add more config if needed
    });

    app.UseRouting();
    app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
}
```

---

### 📈 3. Access Grafana Dashboard

Grafana is preconfigured and accessible via:

```text
http://localhost:3000
```

> Use default credentials (`admin` / `admin`) unless customized.

---

### 🧪 4. Run the Integration Tests

Navigate to the test project folder and run:

```bash
dotnet test
```

Tests cover:
- InfluxDB & Grafana connectivity
- API logging functionality
- Sensitive route exclusion
- Checkpoint system
- Data deletion verification

---

## 📂 Project Structure

```
📦 WatchdogMiddleware
 ┣ 📁 Docker         # Infrastructure (InfluxDB, Grafana, Prometheus...)
 ┣ 📁 Models         # C# classes for intercepted data
 ┣ 📁 Tests          # xUnit-based integration tests
 ┣ 🐶 WatchdogMiddleware.cs # Core middleware logic
 ┣ 📄 .env           # Environment configuration
 ┗ 📄 docker-compose.yml
```

## 🤝 Contact

If you find this useful or want to contribute, feel free to reach out or fork the project!

> Rubén Martínez - [LinkedIn](https://www.linkedin.com/in/ruben-martinez-siso/) | [GitHub](https://github.com/RubenMartinezSiso)

---
