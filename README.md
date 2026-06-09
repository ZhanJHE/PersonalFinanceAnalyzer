# 💰 个人收支趋势分析器

> **课程项目报告** — WPF 桌面应用 + ASP.NET Core 后端服务  
> 实现个人收支的记录、分类、趋势分析与可视化，并通过云端 AI API 提供智能分析建议

---

## 📋 项目信息

| 项目 | 内容 |
|------|------|
| 项目名称 | 个人收支趋势分析器 |
| 技术架构 | 桌面客户端 + 云端后端服务 |
| 客户端框架 | WPF (.NET 9) + MVVM (CommunityToolkit.Mvvm) |
| 后端框架 | ASP.NET Core Web API (.NET 10) |
| 开发工具 | Visual Studio / VS Code + .NET CLI |
| 数据库 | 本地 SQLite（客户端）+ 服务端 SQLite（后端） |
| 图表库 | ScottPlot.WPF 5.x |
| AI 接口 | DeepSeek API（通过后端代理转发） |

---

## 🏗️ 整体架构

```
┌──────────────────────────────────────┐       ┌─────────────────────────────────────┐
│  PersonalFinanceAnalyzer (客户端)     │ HTTP  │  PersonalFinanceAnalyzer.Server     │
│  WPF + MVVM                          │◄─────►│  ASP.NET Core Web API              │
│                                      │       │                                     │
│  离线模式 → 本地 SQLite               │       │  ├─ Auth (JWT 登录/注册)             │
│  在线模式 → 云端同步 + 本地缓存        │       │  ├─ AI 代理 (限额→DeepSeek→返结果)   │
│                                      │       │  ├─ 数据同步 (上传/下载/合并)        │
│  ┌─ 核心功能 ─────────────────────┐  │       │  └─ HTTPS + Rate Limiting           │
│  │ · 收支增删改查                  │  │       └─────────────────────────────────────┘
│  │ · 收支净值柱状图(1/3/6月) / 饼图 │  │
│  │ · AI 智能分析报告               │  │
│  │ · 通用 CSV / 微信账单导入        │  │
│  │ · Excel 导出                    │  │
│  │ · 数据备份 / 恢复               │  │
│  └────────────────────────────────┘  │
└──────────────────────────────────────┘
```

---

## ✅ 功能清单

### 核心功能

| 功能 | 说明 | 状态 |
|------|------|------|
| 收支记录管理 | 新增 / 编辑 / 删除交易记录 | ✅ |
| 分类管理 | 预置 8 种类别 + 用户自定义新增/删除 | ✅ |
| 日期筛选 | 按日期范围筛选记录 | ✅ |
| 文本搜索 | 按备注 / 类别名模糊搜索 | ✅ |
| 月度概览 | 本月收入 / 支出 / 结余卡片 | ✅ |

### 数据导入导出

| 功能 | 说明 | 状态 |
|------|------|------|
| 通用 CSV 导入 | 格式：日期,金额,类别,备注 | ✅ |
| 微信账单导入 | 自动识别微信导出 CSV 并解析 | ✅ |
| 导入使用引导 | 三步图文引导窗口 | ✅ |
| Excel 导出 | ClosedXML 生成 .xlsx，含合计公式 | ✅ |
| 数据备份 | 导出 .db 文件 | ✅ |
| 数据恢复 | 导入 .db 文件覆盖后重启 | ✅ |

### 图表可视化

| 功能 | 说明 | 状态 |
|------|------|------|
| 收支净值柱状图 | 单条净值线+0轴分界（绿=盈余，红=亏损） | ✅ |
| 1/3/6个月范围切换 | 固定15个数据点，自适应时间跨度 | ✅ |
| 支出分类占比饼图 | 当月各分类支出占比，扇区标注百分比 | ✅ |
| SQLite DailyView 视图 | 按天聚合预计算，图表查询性能优化 | ✅ |
| 空状态提示 | 无数据时显示友好文案 | ✅ |

### AI 智能分析（云端）

| 功能 | 说明 | 状态 |
|------|------|------|
| 后端代理转发 | 客户端不直接调 DeepSeek，经后端中转 | ✅ |
| 使用次数限额 | 免费用户 10 次/月，会员 100 次/月 | ✅ |
| AI 仅登录可用 | 未登录时按钮不可用并提示 | ✅ |
| 消费分析 | 1个月/3个月/6个月三种时间范围独立分析 | ✅ |
| Markdown 渲染 | AI 报告支持 Markdown 格式化显示 | ✅ |
| 未登录提示 | 未登录时弹窗询问是否前往登录 | ✅ |

### 账户与安全

| 功能 | 说明 | 状态 |
|------|------|------|
| 用户注册 / 登录 | JWT 认证，密码 BCrypt 哈希 | ✅ |
| Token 持久化 | DPAPI 加密 Token 存本地，启动自动恢复 | ✅ |
| 数据云端同步 | 全量上传/下载，SHA256 哈希比对快速检查 | ✅ |
| 同步冲突解决 | 保留本地/保留云端/逐条处理 三选一 | ✅ |
| 同步状态栏 | 底部状态栏显示同步状态 | ✅ |
| HTTPS 加密 | 客户端与服务器间通信加密 | ✅ |
| Rate Limiting | 登录10次/分钟、注册5次/分钟、AI 30次/分钟 | ✅ |

---

## 🗃️ 数据库设计

### 客户端本地 SQLite

```sql
CREATE TABLE Categories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Type TEXT NOT NULL CHECK(Type IN ('Income', 'Expense')),
    Icon TEXT
);

CREATE TABLE Transactions (
    Id TEXT PRIMARY KEY,                    -- GUID，客户端生成
    Amount REAL NOT NULL,
    Type TEXT NOT NULL CHECK(Type IN ('Income', 'Expense')),
    CategoryId INTEGER NOT NULL,
    TransactionDate TEXT NOT NULL,
    Note TEXT,
    CreatedAt TEXT DEFAULT (datetime('now')),
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
);
```

### 服务端 SQLite (EF Core)

| 表 | 用途 |
|----|------|
| `Users` | 用户账户（用户名 + BCrypt 密码哈希 + 会员标识） |
| `Transactions` | 云端的用户交易记录（与客户端 GUID 一致） |
| `AiUsageLogs` | AI 调用次数统计（用户/年/月） |

---

## 📁 项目结构

```
PersonalFinanceAnalyzer.slnx
│
├── PersonalFinanceAnalyzer/              ← WPF 客户端
│   ├── App.xaml / .cs                    应用入口 + DI 容器
│   ├── MainWindow.xaml / .cs             主窗口（菜单 + 3 Tab + 状态栏）
│   │
│   ├── Models/                           数据模型
│   │   ├── Transaction.cs                Id(Guid), Amount, Type, CategoryId...
│   │   └── Category.cs                   Id, Name, Type, Icon
│   │
│   ├── ViewModels/                       MVVM 视图模型
│   │   ├── MainViewModel.cs              顶层 VM（登录/同步/导入/导出/备份）
│   │   ├── DashboardViewModel.cs         本月收入/支出/结余
│   │   ├── TransactionViewModel.cs       交易 CRUD + 筛选 + 搜索 + 分类管理
│   │   └── AnalysisViewModel.cs          图表数据 + AI 分析
│   │
│   ├── Views/                            XAML 界面
│   │   ├── DashboardView.xaml             概览卡片 + 近期交易
│   │   ├── TransactionView.xaml           新增表单 + 类别管理 + DataGrid
│   │   ├── AnalysisView.xaml              三个独立 Tab 图表
│   │   ├── LoginWindow.xaml              登录/注册弹窗
│   │   ├── AboutWindow.xaml              关于窗口
│   │   ├── AiReportWindow.xaml           AI 报告弹窗
│   │   ├── SyncConflictWindow.xaml       同步冲突窗口
│   │   ├── EditTransactionWindow.xaml     编辑记录弹窗
│   │   └── WeChatImportGuide.xaml         微信导入引导窗口
│   │
│   ├── Services/                         业务逻辑层
│   │   ├── DatabaseService.cs            本地 SQLite CRUD
│   │   ├── TransactionService.cs         离线/在线路由（核心抽象层）
│   │   ├── AuthService.cs                JWT 登录 + DPAPI Token 持久化
│   │   ├── CloudDataService.cs           云端 HTTP API 调用
│   │   ├── SyncService.cs                数据同步（上传/下载/合并）
│   │   ├── ServerAiService.cs            AI 分析（调后端代理）
│   │   ├── ChartService.cs               ScottPlot 图表封装
│   │   ├── CsvImportService.cs           通用 CSV 导入
│   │   ├── WeChatBillParser.cs           微信账单专用解析器
│   │   ├── ExcelExportService.cs         数据导出为 .xlsx
│   │   └── CategoryService.cs            类别管理
│   │
│   ├── Helpers/
│   │   └── Converters.cs                 IntToVis, InverseBoolToVis 转换器
│   │
│   └── appsettings.json                  客户端配置（服务器地址）
│
└── PersonalFinanceAnalyzer.Server/       ← ASP.NET Core 后端
    ├── Program.cs                        启动配置（DI + JWT + HTTPS + Rate Limiting）
    ├── Controllers/
    │   ├── AuthController.cs             注册 / 登录
    │   ├── AiController.cs               AI 代理（限额检查 → DeepSeek → 返结果）
    │   └── SyncController.cs             数据同步（上传 / 下载 / 哈希比对）
    ├── Models/
    │   ├── User.cs                       用户账户
    │   ├── ServerTransaction.cs          云端交易记录
    │   └── AiUsageLog.cs                 AI 使用统计
    ├── Services/
    │   ├── JwtService.cs                 JWT 令牌生成
    │   └── QuotaService.cs               使用次数限额
    ├── Data/
    │   └── AppDbContext.cs               EF Core 数据库上下文
    └── appsettings.json                  服务器配置（JWT Key + DeepSeek API Key）
```

---

## 🚀 运行方式

### 前置条件

1. 安装 .NET 9 SDK 和 .NET 10 SDK（项目客户端用 net9.0，服务端用 net10.0）
2. 信任 HTTPS 开发证书：`dotnet dev-certs https --trust`
3. 在 `PersonalFinanceAnalyzer.Server/appsettings.json` 中填入 DeepSeek API Key

### 启动服务器

```bash
cd PersonalFinanceAnalyzer.Server
dotnet run
# 监听 https://localhost:5001 和 http://localhost:5000
```

### 启动客户端

```bash
cd PersonalFinanceAnalyzer
dotnet run
```

### 首次使用流程

1. 启动客户端 → 点击「用户 → 登录...」→「注册」创建账户
2. 注册后自动登录，数据自动同步
3. 在「记录」Tab 新增收支记录
4. 在「概览」Tab 查看月度汇总
5. 在「分析」Tab 查看图表和 AI 分析报告

---

## 🛡️ 安全措施

| 措施 | 说明 |
|------|------|
| BCrypt 密码哈希 | 服务端存储密码哈希，不存明文 |
| JWT 认证 | 登录后签发 Token，过期自动失效 |
| HTTPS 加密 | 通信全程加密 |
| Rate Limiting | 登录/注册/AI 端点限流，防暴力攻击 |
| DPAPI 加密 | Token 加密存本地，不落明文 |
| API Key 服务端保管 | DeepSeek Key 存在服务端，客户端不接触 |

---

## 📚 参考资源

- [CommunityToolkit.Mvvm 文档](https://learn.microsoft.com/zh-cn/dotnet/communitytoolkit/mvvm/)
- [ScottPlot WPF 快速入门](https://scottplot.net/quickstart/wpf/)
- [Microsoft.Data.Sqlite 文档](https://learn.microsoft.com/zh-cn/dotnet/standard/data/sqlite/)
- [ASP.NET Core 官方文档](https://learn.microsoft.com/zh-cn/aspnet/core/)
- [ClosedXML 文档](https://closedxml.github.io/ClosedXML/)
