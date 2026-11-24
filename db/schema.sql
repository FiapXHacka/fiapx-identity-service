CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261123195233_InitialCreate') THEN
    CREATE TABLE outbox_messages (
        id uuid NOT NULL,
        type text NOT NULL,
        payload jsonb NOT NULL,
        occurred_at timestamp with time zone NOT NULL DEFAULT (now()),
        published_at timestamp with time zone,
        attempts integer NOT NULL DEFAULT 0,
        CONSTRAINT pk_outbox_messages PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261123195233_InitialCreate') THEN
    CREATE TABLE users (
        id uuid NOT NULL,
        email text NOT NULL,
        name text NOT NULL,
        password_hash text NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_users PRIMARY KEY (id),
        CONSTRAINT users_email_format CHECK (position('@' in email) > 1)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261123195233_InitialCreate') THEN
    CREATE INDEX ix_outbox_pending ON outbox_messages (occurred_at) WHERE published_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261123195233_InitialCreate') THEN
    CREATE UNIQUE INDEX ux_users_email_lower ON users (lower(email));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261123195233_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261123195233_InitialCreate', '10.0.15');
    END IF;
END $EF$;
COMMIT;

