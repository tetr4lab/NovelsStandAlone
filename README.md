---
title: Blazor Web App (Server) から Blazor Hybrid (WPF) への移植例
tags: epub webscraping smtp-mail blazor-hybrid wpf
---

# Blazor Web App (Server) から Blazor Hybrid (WPF) への移植例
## はじめに
- これは、実証実験のために、Blazor Web App (Server) を Blazor Hybrid (WPF) に移植し、スタンドアロンにしたものです。
- 元の Blazor Web App (Server) は以下にあります。
  - https://github.com/tetr4lab/Novels
- 移植の参考記事は以下にあります。
  - https://zenn.dev/tetr4lab/articles/10f1770dad1962

## 概要
認証を除去し、DB周りを書き換えました。
ロジックやUIは触っていません。

### 相違点
- プラットフォーム
    - 旧: .net core server / modern browsers
    - 新: Windows
- データベース
    - 旧: MySql/MariaDB
    - 新: SQLite
- 認証
    - 旧: Google OAuth
    - 新: なし

### 留意事項
- コードを流用しただけで、元リポジトリをフォークしたわけではありません。
- テストは行われておらず、不具合がある可能性があります。
    - 不具合をお知らせいただいた場合でも、修正されない可能性があります。
        - 修正される可能性を否定するものではありません。
    - 将来的に保守されない可能性が高いです。
      - 元リポジトリよりも古くなっている可能性があります。

## おわりに
お読みいただきありがとうございました。
