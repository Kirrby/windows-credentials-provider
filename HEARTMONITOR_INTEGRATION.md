# HeartMonitor Windows Credential Provider

## 项目概述

此项目是一个基于 C# 的 Windows 凭据提供程序 (Credential Provider)，集成了 HeartMonitor 手机心跳检测系统，实现手机靠近自动解锁功能。

## 功能特点

1. **手机解锁凭据项**：在 Windows 登录界面显示 "Phone Unlock" 凭据项
2. **状态感知**：动态响应 HeartMonitor 状态文件变化
3. **智能显示控制**：根据手机在线状态显示或隐藏解锁凭据
4. **安全验证**：解锁前再次验证手机状态
5. **状态轮询**：定时检查 HeartMonitor 状态变化 (每 5 秒)

## 实现细节

### HeartMonitor 集成
- 读取路径：`C:\ProgramData\HeartMonitor\state.json`
- 解析字段：`allow_unlock`（主要控制字段）、`version`、`last_update_utc`、`primary_device_id`
- 使用正则表达式解析，避免依赖外部 JSON 库

### 凭据显示逻辑
- 当 `allow_unlock` 为 `true` 时：
  - 显示 "Phone Unlock (Available)" 和 "Unlock with Phone" 
  - 允许用户点击解锁
- 当 `allow_unlock` 为 `false` 时：
  - 隐藏凭据项或显示禁用状态
  - 阻止解锁尝试

### 安全验证
- 用户点击解锁按钮时，再次检查 HeartMonitor 状态
- 仅当手机仍在线时才允许解锁
- 提供相应的错误信息

### 状态轮询
- 每 5 秒自动检查 HeartMonitor 状态文件
- 状态变化时更新 UI 标签
- 保持界面实时响应

## 文件结构

```
WindowsCredentialProviderTest/
├── HeartMonitorState.cs      # HeartMonitor 状态管理
├── TestWindowsCredentialProviderTile.cs  # 主凭据提供程序实现
├── TestWindowsCredentialProvider.cs      # 凭据提供程序容器
└── ...                     # 其他原有文件
```

## 部署说明

1. 将编译后的 DLL 注册为 COM 组件
2. 将以下注册表项添加到 `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{298D9F84-9BC5-435C-9FC2-EB3746625954}`
3. 确保 HeartMonitor 程序能够写入 `C:\ProgramData\HeartMonitor\state.json`

## 注意事项

- 此功能修改系统登录界面，测试前请确保有备用登录方式
- 确保 `C:\ProgramData\HeartMonitor\` 目录存在且可读
- 产品环境中应使用更安全的身份验证机制

## 测试方法

修改 `C:\ProgramData\HeartMonitor\state.json` 文件中的 `allow_unlock` 字段值，验证界面凭据项是否相应变化。