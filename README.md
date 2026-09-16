# VideoCheckTool

Windows 桌面媒體檢視與快速分類工具。可在同一個工作區瀏覽來源資料夾、預覽影片或照片，並用鍵盤將檔案搬移到 A／D 分類資料夾。

## 功能

- 選擇來源資料夾，可切換是否包含子資料夾
- 支援影片播放、暫停、時間軸拖曳，以及照片預覽
- `A`／`D`：將目前預覽檔案搬到對應分類資料夾
- `←`／`→`：上一個／下一個檔案
- `Space`：播放／暫停影片
- 設定要納入的影片與照片副檔名，可同時選取兩類
- 右側即時顯示 A／D 資料夾內容
- 在右側檔案按右鍵，可改分到另一類或還原原始路徑
- 自動處理同名檔案，不覆寫既有檔案
- 搬移紀錄保存在 `%LocalAppData%\VideoCheckTool`，重開程式後仍可還原

## 執行需求

- Windows 10/11
- .NET 8 Desktop Runtime

開發環境可執行：

```powershell
dotnet run --project .\VideoCheckTool\VideoCheckTool.csproj
```

影片播放使用 Windows 內建媒體解碼器；實際可播放的編碼格式取決於系統已安裝的 codec。
