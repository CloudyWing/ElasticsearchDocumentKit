---
status: accepted
date: 2026-08-20
---

# ADR-0001：公開面以 PublicAPI 文字檔宣告，未宣告即建置失敗

## Context

本套件首次發佈前需決定以何種機制守住公開 API 的相容性承諾。可選機制有三：僅靠 code review、僅靠 `EnablePackageValidation` 比對已發佈 baseline、以 `Microsoft.CodeAnalysis.PublicApiAnalyzers` 將公開面固化為版控中的文字檔。套件尚未發佈，`EnablePackageValidation` 在首版時無 baseline 可比對。

## Decision

採用 `PublicApiAnalyzers`，公開成員全量寫入 `PublicAPI.Shipped.txt`，並將 `RS0016`（未宣告的公開成員）與 `RS0017`（已宣告但不存在的成員）設為建置錯誤，`RS0026`（可選參數多載）列入 `NoWarn`。

## Consequences

任何新增或移除公開成員都會使建置失敗，直到開發者明確更新 `PublicAPI.Unshipped.txt`，公開面的變動因此必然出現在 diff 中而無法無聲發生。此機制在無 baseline 的首版即可運作，與 `EnablePackageValidation` 正交，後者待首版發佈後另行啟用並填入 `PackageValidationBaselineVersion`。
