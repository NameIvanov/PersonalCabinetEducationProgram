USE personal_cabinet;

ALTER TABLE roles
    ADD COLUMN normalized_name VARCHAR(100) NULL,
    ADD COLUMN concurrency_stamp LONGTEXT NULL;

ALTER TABLE users
    ADD COLUMN normalized_username VARCHAR(100) NULL,
    ADD COLUMN email VARCHAR(256) NULL,
    ADD COLUMN normalized_email VARCHAR(256) NULL,
    ADD COLUMN email_confirmed TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN security_stamp LONGTEXT NULL,
    ADD COLUMN concurrency_stamp LONGTEXT NULL,
    ADD COLUMN phone_number LONGTEXT NULL,
    ADD COLUMN phone_confirmed TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN two_factor_enabled TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN lockout_end DATETIME(6) NULL,
    ADD COLUMN lockout_enabled TINYINT(1) NOT NULL DEFAULT 1,
    ADD COLUMN access_failed_count INT NOT NULL DEFAULT 0;

UPDATE roles
SET normalized_name = UPPER(Name),
    concurrency_stamp = COALESCE(concurrency_stamp, UUID());

UPDATE users
SET normalized_username = UPPER(username),
    security_stamp = COALESCE(security_stamp, UUID()),
    concurrency_stamp = COALESCE(concurrency_stamp, UUID());

CREATE TABLE user_roles (
    user_id INT NOT NULL,
    role_id INT NOT NULL,
    CONSTRAINT pk_user_roles PRIMARY KEY (user_id, role_id),
    CONSTRAINT fk_ur_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT fk_ur_role FOREIGN KEY (role_id) REFERENCES roles (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX ix_ur_role ON user_roles (role_id);

INSERT INTO user_roles (user_id, role_id)
SELECT Id, role_id
FROM users
WHERE role_id IS NOT NULL;

ALTER TABLE users MODIFY role_id INT NULL;

CREATE TABLE user_claims (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    claim_type LONGTEXT NULL,
    claim_value LONGTEXT NULL,
    CONSTRAINT fk_uc_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX ix_uc_user ON user_claims (user_id);

CREATE TABLE role_claims (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    role_id INT NOT NULL,
    claim_type LONGTEXT NULL,
    claim_value LONGTEXT NULL,
    CONSTRAINT fk_rc_role FOREIGN KEY (role_id) REFERENCES roles (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX ix_rc_role ON role_claims (role_id);

CREATE TABLE user_logins (
    login_provider VARCHAR(128) NOT NULL,
    provider_key VARCHAR(128) NOT NULL,
    provider_name LONGTEXT NULL,
    user_id INT NOT NULL,
    CONSTRAINT pk_user_logins PRIMARY KEY (login_provider, provider_key),
    CONSTRAINT fk_ul_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX ix_ul_user ON user_logins (user_id);

CREATE TABLE user_tokens (
    user_id INT NOT NULL,
    login_provider VARCHAR(128) NOT NULL,
    name VARCHAR(128) NOT NULL,
    value LONGTEXT NULL,
    CONSTRAINT pk_user_tokens PRIMARY KEY (user_id, login_provider, name),
    CONSTRAINT fk_ut_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE UNIQUE INDEX ux_users_name ON users (normalized_username);
CREATE INDEX ix_users_email ON users (normalized_email);
CREATE UNIQUE INDEX ux_roles_name ON roles (normalized_name);
