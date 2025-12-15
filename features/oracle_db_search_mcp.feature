Feature: Oracle DB Searching MCP
  作為一個使用 MCP 的 agent
  我希望能透過 MCP 工具安全地查詢 Oracle 資料庫（以 SELECT 為主），並支援基本的 schema 搜尋
  以便我能快速理解資料庫結構與取得查詢結果（含欄位/資料列），同時避免誤執行寫入操作

  Background:
    Given MCP server 已啟動並註冊 Oracle DB 相關工具

  Scenario: 成功連線並檢查資料庫可用性
    Given 設定了 ORACLE_CONNECTION_STRING 環境變數
    And 執行環境可載入 Oracle .NET driver（Oracle.ManagedDataAccess）
    When 呼叫 oracle_ping
    Then 回傳狀態為 ok
    And 回傳包含 db 版本或標識資訊（若可取得）

  Scenario: 未設定連線字串時回報可理解的錯誤
    Given 未設定 ORACLE_CONNECTION_STRING 環境變數
    When 呼叫 oracle_ping
    Then 回傳錯誤碼為 missing_connection_string
    And 錯誤訊息指示需設定 ORACLE_CONNECTION_STRING

  Scenario: 未安裝/無法載入 Oracle driver 時回報可理解的錯誤
    Given 設定了 ORACLE_CONNECTION_STRING 環境變數
    And 執行環境無法載入 Oracle .NET driver（Oracle.ManagedDataAccess）
    When 呼叫 oracle_ping
    Then 回傳錯誤碼為 missing_oracle_driver
    And 錯誤訊息指示需安裝 Oracle.ManagedDataAccess（或相容 driver）

  Scenario: 執行 SELECT 查詢並以 max_rows 限制結果
    Given 設定了 ORACLE_CONNECTION_STRING 環境變數
    And 執行環境可載入 Oracle .NET driver（Oracle.ManagedDataAccess）
    When 呼叫 oracle_query 並提供 sql 為 "select * from SOME_TABLE"
    And 提供 max_rows 為 50
    Then 回傳 rows 數量小於等於 50
    And 回傳包含 columns（欄位名稱、資料型別）

  Scenario: 支援 bind 參數避免字串拼接
    Given 設定了 ORACLE_CONNECTION_STRING 環境變數
    And 執行環境可載入 Oracle .NET driver（Oracle.ManagedDataAccess）
    When 呼叫 oracle_query 並提供 sql 為 "select * from SOME_TABLE where ID = :id"
    And 提供 parameters 為 { "id": 123 }
    Then 查詢成功
    And 回傳包含 columns 與 rows

  Scenario: 拒絕非唯讀 SQL（DML/DDL/PLSQL）
    Given 設定了 ORACLE_CONNECTION_STRING 環境變數
    When 呼叫 oracle_query 並提供 sql 為 "delete from SOME_TABLE"
    Then 回傳錯誤碼為 non_readonly_sql
    And 不應對資料庫造成任何變更

  Scenario: schema 搜尋可找到符合關鍵字的表與欄位
    Given 設定了 ORACLE_CONNECTION_STRING 環境變數
    And 執行環境可載入 Oracle .NET driver（Oracle.ManagedDataAccess）
    When 呼叫 oracle_search_schema 並提供 keyword 為 "USER"
    Then 回傳包含符合關鍵字的 tables 或 columns
    And 每個結果包含 owner、table_name 與（若為欄位）column_name

  Scenario: SQL 語法錯誤時回報可理解的錯誤
    Given 設定了 ORACLE_CONNECTION_STRING 環境變數
    And 執行環境可載入 Oracle .NET driver（Oracle.ManagedDataAccess）
    When 呼叫 oracle_query 並提供 sql 為 "select * from"
    Then 回傳錯誤碼為 query_failed
    And 錯誤訊息包含 Oracle 錯誤摘要（不包含連線字串）
