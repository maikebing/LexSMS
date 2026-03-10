# 发布 NuGet 包指南

本文档说明如何使用 GitHub Actions 自动发布 LexSMS 到 NuGet.org。

## 前置准备

### 1. 获取 NuGet API Key

1. 访问 [NuGet.org](https://www.nuget.org/)
2. 登录你的账号
3. 点击右上角的用户名，选择 "API Keys"
4. 点击 "Create" 创建新的 API Key
5. 填写以下信息：
   - **Key Name**: `GitHub Actions - LexSMS`
   - **Expiration**: 选择过期时间（建议 365 天）
   - **Scopes**: 选择 "Push" 和 "Push new packages and package versions"
   - **Packages**: 选择 "LexSMS" 或 "*" (所有包)
6. 复制生成的 API Key（只显示一次，请妥善保存）

### 2. 配置 GitHub Secrets

1. 访问你的 GitHub 仓库
2. 点击 "Settings" > "Secrets and variables" > "Actions"
3. 点击 "New repository secret"
4. 添加以下 secret：
   - **Name**: `NUGET_API_KEY`
   - **Value**: 粘贴你在上一步获得的 NuGet API Key
5. 点击 "Add secret"

## 发布流程

### 自动发布

当你推送一个以 `v` 开头的 tag 时，GitHub Actions 会自动：

1. 编译项目
2. 运行测试
3. 打包 NuGet 包
4. 发布到 NuGet.org
5. 创建 GitHub Release
6. 上传 NuGet 包到 Release 页面

### 发布步骤

1. **确保代码已提交并推送**
   ```bash
   git add .
   git commit -m "准备发布版本 1.0.0"
   git push origin main
   ```

2. **创建并推送 tag**
   ```bash
   # 创建 tag（版本号根据实际情况修改）
   git tag v1.0.0
   
   # 或者创建带注释的 tag
   git tag -a v1.0.0 -m "Release version 1.0.0"
   
   # 推送 tag 到远程仓库
   git push origin v1.0.0
   ```

3. **查看发布进度**
   - 访问仓库的 "Actions" 页面
   - 查看 "Publish NuGet Package" workflow 的运行状态

4. **验证发布结果**
   - 检查 [NuGet.org](https://www.nuget.org/packages/LexSMS/) 上是否有新版本
   - 检查仓库的 "Releases" 页面是否创建了新的发布

## 版本号规范

建议遵循 [语义化版本](https://semver.org/lang/zh-CN/) 规范：

- **主版本号 (Major)**: 不兼容的 API 修改
- **次版本号 (Minor)**: 向下兼容的功能性新增
- **修订号 (Patch)**: 向下兼容的问题修正

示例：
- `v1.0.0` - 首个正式版本
- `v1.1.0` - 添加新功能
- `v1.1.1` - 修复 bug
- `v2.0.0` - 重大更新，可能包含不兼容变更

## 预发布版本

如果需要发布预发布版本（如 alpha、beta、rc），可以这样命名：

```bash
git tag v1.0.0-alpha
git tag v1.0.0-beta.1
git tag v1.0.0-rc.1
git push origin v1.0.0-alpha
```

## 故障排除

### 发布失败

1. **检查 NUGET_API_KEY**
   - 确保 Secret 已正确配置
   - 确保 API Key 未过期
   - 确保 API Key 有足够的权限

2. **检查版本号**
   - 确保版本号未被使用过
   - NuGet.org 不允许覆盖已发布的版本

3. **检查测试**
   - 如果测试失败，发布会中止
   - 可以在本地运行 `dotnet test` 确保测试通过

### 手动发布

如果自动发布失败，你也可以手动发布：

```bash
# 1. 打包
dotnet pack src/LexSMS/LexSMS.csproj -c Release -p:PackageVersion=1.0.0

# 2. 发布到 NuGet.org
dotnet nuget push src/LexSMS/bin/Release/LexSMS.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Workflow 文件说明

Workflow 配置文件位于：`.github/workflows/publish-nuget.yml`

主要步骤：
1. **Checkout code**: 检出代码
2. **Setup .NET**: 安装 .NET 8.0
3. **Extract version**: 从 tag 中提取版本号
4. **Restore & Build**: 恢复依赖并编译
5. **Test**: 运行测试
6. **Pack**: 打包 NuGet 包
7. **Publish to NuGet**: 发布到 NuGet.org
8. **Create Release**: 创建 GitHub Release
9. **Upload Asset**: 上传包到 Release

## 注意事项

1. Tag 必须以 `v` 开头，例如 `v1.0.0`
2. 版本号会自动从 tag 中提取（去掉开头的 `v`）
3. 发布到 NuGet.org 后，包可能需要几分钟才会在搜索中显示
4. 已发布的版本无法删除或覆盖，请谨慎发布
5. 建议在发布前先在本地测试打包：`dotnet pack -c Release`
