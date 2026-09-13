# PCL-In

> 基於 **PCL Community Edition (PCL CE)** 的二次 fork。

PCL-In 是一個為非正版使用者(尤其是第三方皮膚站使用者)提供更好體驗的 Minecraft 啟動器。

## 與上游 PCL CE 的差異

- **去除強制正版驗證**:使用第三方皮膚站(Authlib / YggdrasilConnect)或離線檔案的使用者,不再被強制進入「試玩模式」。
- **去除所有贊助提示**:不彈出開助助窗,不引導使用者訪問原作者的愛發電。
- **更新機制獨立**:透過 GitHub Releases API 偵測本倉庫的新版本,不會覆蓋你的 fork 改動;偵測失敗時會如實報錯,不會假裝「已是最新版本」。
- **內嵌聊天室**:頂部導覽新增「聊天」分頁,內嵌 [MiniChat](https://minichat.astras.cc),不必另外開瀏覽器;不需要時可在「設定 → 功能隱藏」中單獨關閉。
- **連線更抗斷**:未設定公告伺服器、或公告伺服器連不上時,不再直接判定連線功能不可用,而是回退到本機預設值,仍可用 EasyTier / 陶瓦進行 P2P 連線。
- **介面細節**:頂欄與左側欄都能切換成僅顯示圖示;捲軸預設隱藏,滑鼠移上去才顯示。

## 下載

前往 [Releases](https://github.com/PCL-In/desktop/releases) 頁面下載最新版本:64 位元系統選 `PCL-In-x64.exe`,ARM64 選 `PCL-In-arm64.exe`。

每個安裝包都附帶 `.sha256` 校驗值與 GPG 簽章 `.asc`,可自行校驗。

## 系統需求

- Windows 10 1809 (17763) 或更高
- [.NET 10 Desktop Runtime](https://get.dot.net/10)
- (選用)內嵌聊天室需要 [WebView2 執行階段](https://developer.microsoft.com/microsoft-edge/webview2/):Windows 11 與裝有新式 Edge 的 Windows 10 已內建;缺少時聊天頁會提示改用系統瀏覽器開啟,不影響其他功能

## 致謝

本專案基於 [PCL Community Edition (PCL CE)](https://github.com/PCL-Community/PCL-CE) 開發,所有權利歸原作者 [龍騰貓躍](https://github.com/Meloong-Git/PCL) 及 [成都瓜皮龍科技有限公司](https://www.pclc.cc/) 所有。

上游第三方元件的版權資訊詳見 [`Plain Craft Launcher 2/metadata.json`](./Plain%20Craft%20Launcher%202/metadata.json) 的 `licenses[]` 陣列。

## 授權

- 根目錄 [`LICENSE`](./LICENSE) 遵循 Apache License 2.0
- [`Plain Craft Launcher 2/LICENCE`](./Plain%20Craft%20Launcher%202/LICENCE) 遵循上游《PCL 分發有限許可》
