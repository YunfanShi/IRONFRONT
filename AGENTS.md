# 项目协作要求

每次开始任务前核对本机 main 与 origin/main 的版本和提交，先保持一致再修改。
任务结束后将已验证的代码和详细 DOCX 任务说明提交到 main 并上传仓库，不随意创建分支。

版本号规则：
- 小问题修复：最后一位加一。
- 新功能或新模块：中间位加一，最后一位归零。
- 大改版或平台重构：第一位加一。

音乐资源保留在本机 public/audio/music，沿用 .gitignore 不提交用户 MP3。
Windows 与 Mac 不共用 node_modules、dist 或 .logic-build；在各自系统安装与构建。
