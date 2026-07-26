# Agent 约定

## 禁止通过 build 验证

AI **不得**使用 `dotnet build`、`msbuild` 或其他编译命令来验证改动是否可用。

开发时由开发者自行运行与检查：

```powershell
dotnet watch run --project .\src\BentoDesk\BentoDesk.csproj -c Debug -property:Platform=x64
```

AI 也不应主动执行上述 `dotnet watch run`；该命令仅供开发者本地使用。
