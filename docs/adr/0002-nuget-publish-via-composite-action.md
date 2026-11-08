---
status: accepted
date: 2026-08-20
---

# ADR-0002：發佈步驟以 composite action 呼叫，不使用 reusable workflow

## Context

發佈流程的其餘環節（建立 Release、部署文件）皆以 `CloudyWing/devops-templates` 的 reusable workflow 共用，發佈至 nuget.org 的步驟依同樣形式共用是直覺選擇。但 nuget.org 的 Trusted Publishing 會驗證 OIDC token 的 `job_workflow_ref` claim，要求其前綴為本 repository 自身的 workflow 路徑。

## Decision

`publish-nuget` 與 `probe` 兩個 job 定義在本 repository 的 `release.yml` 內，發佈步驟以 `devops-templates` 的 `nuget-publish` composite action 呼叫，不改用 reusable workflow。

## Consequences

reusable workflow 會使 `job_workflow_ref` 指向 `devops-templates` 而導致 Trusted Publishing 驗證失敗，此限制使發佈 job 的骨架必須在每個套件 repository 各寫一份，共用僅止於步驟層級。日後若有人為了與其他 job 形式一致而將發佈改為 reusable workflow，發佈會在憑證兌換階段失敗，故 `release.yml` 內需保留說明此約束的註解。
