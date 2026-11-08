# 架構決策紀錄

本目錄保存影響公開 API 與發佈流程的架構決策。每份 ADR 記錄決策背景、採用方案與維護時必須遵守的限制。

## 已接受的決策

| ADR | 決策 | 摘要 |
| --- | --- | --- |
| [ADR-0001](0001-public-api-declaration-as-build-error.md) | 公開 API 以 PublicAPI 文字檔宣告 | 以 `RS0016` 與 `RS0017` 作為建置錯誤，讓公開面變更先經過文字清單審核。 |
| [ADR-0002](0002-nuget-publish-via-composite-action.md) | 發佈步驟以 composite action 呼叫 | 維持 Trusted Publishing 所需的 OIDC workflow claim。 |

## 維護方式

新增長期架構決策時，建立下一個序號的 ADR，並同步更新本頁與 `docs/adr/toc.yml`。既有 ADR 保留原始決策內容；後續決策以新文件記錄取代關係。
