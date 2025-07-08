DROP TABLE IF EXISTS "books";
CREATE TABLE "books" (
  "id" integer NOT NULL,
  "version" integer NOT NULL DEFAULT 0,
  "created" datetime NOT NULL DEFAULT (DATETIME('now', 'localtime')),
  "creator" text NOT NULL DEFAULT '',
  "modified" datetime NOT NULL DEFAULT (DATETIME('now', 'localtime')),
  "modifier" text NOT NULL DEFAULT '',
  "url1" text NOT NULL DEFAULT '',
  "url2" text NOT NULL DEFAULT '',
  "html" text DEFAULT NULL,
  "site" integer NOT NULL DEFAULT 0,
  "title" text DEFAULT NULL,
  "author" text DEFAULT NULL,
  "number_of_published" integer DEFAULT NULL,
  "published_at" datetime DEFAULT NULL,
  "read" integer NOT NULL DEFAULT 0,
  "memorandum" text DEFAULT NULL,
  "status" text NOT NULL DEFAULT '',
  "html_backup" text DEFAULT NULL,
  "errata" text DEFAULT NULL,
  "wish" integer NOT NULL DEFAULT 0,
  "bookmark" integer DEFAULT NULL,
  "remarks" text DEFAULT NULL,
  PRIMARY KEY ("id" AUTOINCREMENT)
);

DROP TRIGGER IF EXISTS "books_update_modified";
CREATE TRIGGER "books_update_modified"
  AFTER UPDATE ON "books"
  FOR EACH ROW
  WHEN NEW."version" > OLD."version" AND NEW."modified" = OLD."modified"
BEGIN
  UPDATE "books"
  SET modified = DATETIME('now', 'localtime')
  WHERE id = NEW.id;
END;

DROP TRIGGER IF EXISTS "books_version_check";
CREATE TRIGGER "books_version_check"
  BEFORE UPDATE ON "books"
  FOR EACH ROW
  WHEN NEW."version" <= OLD."version" AND NOT (NEW."version" = OLD."version" AND NEW."modified" > OLD."modified")
BEGIN
  SELECT RAISE(ABORT, 'Version mismatch detected.');
END;

DROP TABLE IF EXISTS "settings";
CREATE TABLE "settings" (
  "id" integer NOT NULL,
  "version" integer NOT NULL DEFAULT 0,
  "created" datetime NOT NULL DEFAULT (DATETIME('now', 'localtime')),
  "creator" text NOT NULL DEFAULT '',
  "modified" datetime NOT NULL DEFAULT (DATETIME('now', 'localtime')),
  "modifier" text NOT NULL DEFAULT '',
  "personal_document_limit_size" integer NOT NULL DEFAULT 0,
  "smtp_mailaddress" text NOT NULL DEFAULT '',
  "smtp_server" text NOT NULL DEFAULT '',
  "smtp_port" integer NOT NULL DEFAULT 25,
  "smtp_username" text NOT NULL DEFAULT '',
  "smtp_password" text NOT NULL DEFAULT '',
  "smtp_mailto" text NOT NULL DEFAULT '',
  "smtp_cc" text NOT NULL DEFAULT '',
  "smtp_bcc" text NOT NULL DEFAULT '',
  "smtp_subject" text NOT NULL DEFAULT '',
  "smtp_body" text NOT NULL DEFAULT '',
  "user_agent" text NOT NULL DEFAULT 'Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko',
  "access_interval_time" integer NOT NULL DEFAULT 1000,
  "default_cookies" text NOT NULL DEFAULT '{ "over18": "yes" }',
  "remarks" text DEFAULT NULL,
  "include_image" integer NOT NULL DEFAULT 0,
  PRIMARY KEY ("id" AUTOINCREMENT)
);

DROP TRIGGER IF EXISTS "settings_update_modified";
CREATE TRIGGER "settings_update_modified"
  AFTER UPDATE ON "settings"
  FOR EACH ROW
  WHEN NEW."version" > OLD."version" AND NEW."modified" = OLD."modified"
BEGIN
  UPDATE "settings"
  SET modified = DATETIME('now', 'localtime')
  WHERE id = NEW.id;
END;

DROP TRIGGER IF EXISTS "settings_version_check";
CREATE TRIGGER "settings_version_check"
  BEFORE UPDATE ON "settings"
  FOR EACH ROW
  WHEN NEW."version" <= OLD."version" AND NOT (NEW."version" = OLD."version" AND NEW."modified" > OLD."modified")
BEGIN
  SELECT RAISE(ABORT, 'Version mismatch detected.');
END;

DROP TABLE IF EXISTS "sheets";
CREATE TABLE "sheets" (
  "id" integer NOT NULL,
  "version" integer NOT NULL DEFAULT 0,
  "created" datetime NOT NULL DEFAULT (DATETIME('now', 'localtime')),
  "creator" text NOT NULL DEFAULT '',
  "modified" datetime NOT NULL DEFAULT (DATETIME('now', 'localtime')),
  "modifier" text NOT NULL DEFAULT '',
  "book_id" integer NOT NULL,
  "url" text NOT NULL DEFAULT '',
  "html" text DEFAULT NULL,
  "sheet_update" datetime DEFAULT NULL,
  "novel_no" integer NOT NULL DEFAULT 0,
  "errata" text DEFAULT NULL,
  "remarks" text DEFAULT NULL,
  PRIMARY KEY ("id" AUTOINCREMENT),
  CONSTRAINT "fk_bookid_books_id" FOREIGN KEY ("book_id") REFERENCES "books" ("id") ON DELETE CASCADE
);

DROP TRIGGER IF EXISTS "sheets_update_modified";
CREATE TRIGGER "sheets_update_modified"
  AFTER UPDATE ON "sheets"
  FOR EACH ROW
  WHEN NEW."version" > OLD."version" AND NEW."modified" = OLD."modified"
BEGIN
  UPDATE "sheets"
  SET modified = DATETIME('now', 'localtime')
  WHERE id = NEW.id;
END;

DROP TRIGGER IF EXISTS "sheets_version_check";
CREATE TRIGGER "sheets_version_check"
  BEFORE UPDATE ON "sheets"
  FOR EACH ROW
  WHEN NEW."version" <= OLD."version" AND NOT (NEW."version" = OLD."version" AND NEW."modified" > OLD."modified")
BEGIN
  SELECT RAISE(ABORT, 'Version mismatch detected.');
END;
