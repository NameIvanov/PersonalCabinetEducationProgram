SET NAMES utf8mb4;

CREATE DATABASE IF NOT EXISTS personal_cabinet
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE personal_cabinet;

CREATE TABLE IF NOT EXISTS roles (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100) NULL,
    normalized_name VARCHAR(100) NULL,
    concurrency_stamp LONGTEXT NULL,
    Description LONGTEXT NOT NULL,
    CONSTRAINT ux_roles_name UNIQUE (normalized_name)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS users (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(100) NULL,
    normalized_username VARCHAR(100) NULL,
    email VARCHAR(256) NULL,
    normalized_email VARCHAR(256) NULL,
    email_confirmed TINYINT(1) NOT NULL DEFAULT 0,
    password_hash LONGTEXT NULL,
    security_stamp LONGTEXT NULL,
    concurrency_stamp LONGTEXT NULL,
    phone_number LONGTEXT NULL,
    phone_confirmed TINYINT(1) NOT NULL DEFAULT 0,
    two_factor_enabled TINYINT(1) NOT NULL DEFAULT 0,
    lockout_end DATETIME(6) NULL,
    lockout_enabled TINYINT(1) NOT NULL DEFAULT 1,
    access_failed_count INT NOT NULL DEFAULT 0,
    full_name LONGTEXT NOT NULL,
    post LONGTEXT NOT NULL,
    approval_status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    rejection_reason LONGTEXT NULL,
    role_id INT NULL,
    CONSTRAINT ux_users_name UNIQUE (normalized_username),
    CONSTRAINT uq_users_username UNIQUE (username),
    CONSTRAINT fk_users_role_legacy FOREIGN KEY (role_id)
        REFERENCES roles (Id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE INDEX ix_users_email ON users (normalized_email);

CREATE TABLE IF NOT EXISTS user_roles (
    user_id INT NOT NULL,
    role_id INT NOT NULL,
    CONSTRAINT pk_user_roles PRIMARY KEY (user_id, role_id),
    CONSTRAINT fk_ur_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT fk_ur_role FOREIGN KEY (role_id) REFERENCES roles (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX ix_ur_role ON user_roles (role_id);

CREATE TABLE IF NOT EXISTS user_claims (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    claim_type LONGTEXT NULL,
    claim_value LONGTEXT NULL,
    CONSTRAINT fk_uc_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX ix_uc_user ON user_claims (user_id);

CREATE TABLE IF NOT EXISTS role_claims (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    role_id INT NOT NULL,
    claim_type LONGTEXT NULL,
    claim_value LONGTEXT NULL,
    CONSTRAINT fk_rc_role FOREIGN KEY (role_id) REFERENCES roles (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX ix_rc_role ON role_claims (role_id);

CREATE TABLE IF NOT EXISTS user_logins (
    login_provider VARCHAR(128) NOT NULL,
    provider_key VARCHAR(128) NOT NULL,
    provider_name LONGTEXT NULL,
    user_id INT NOT NULL,
    CONSTRAINT pk_user_logins PRIMARY KEY (login_provider, provider_key),
    CONSTRAINT fk_ul_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX ix_ul_user ON user_logins (user_id);

CREATE TABLE IF NOT EXISTS user_tokens (
    user_id INT NOT NULL,
    login_provider VARCHAR(128) NOT NULL,
    name VARCHAR(128) NOT NULL,
    value LONGTEXT NULL,
    CONSTRAINT pk_user_tokens PRIMARY KEY (user_id, login_provider, name),
    CONSTRAINT fk_ut_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS facultys (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name LONGTEXT NOT NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS departments (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    code_department LONGTEXT NULL,
    Name LONGTEXT NOT NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS educational_programs (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    code_referral LONGTEXT NOT NULL,
    Name LONGTEXT NOT NULL,
    educational_level LONGTEXT NOT NULL,
    year_approvals INT NULL,
    Status LONGTEXT NOT NULL,
    user_id INT NULL,
    CONSTRAINT fk_prog_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS educational_program_elements (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    educational_program_id INT NOT NULL,
    type_element LONGTEXT NOT NULL,
    Name LONGTEXT NOT NULL,
    upload_date DATE NULL,
    Description LONGTEXT NOT NULL,
    status_approvals LONGTEXT NOT NULL,
    file_path LONGTEXT NULL,
    file_name LONGTEXT NULL,
    CONSTRAINT fk_elem_prog FOREIGN KEY (educational_program_id)
        REFERENCES educational_programs (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS comments_educational_program_element (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    educational_program_element_id INT NOT NULL,
    user_id INT NOT NULL,
    date_time_comment DATETIME(6) NOT NULL,
    comment_content LONGTEXT NOT NULL,
    Status LONGTEXT NOT NULL,
    CONSTRAINT fk_comm_elem FOREIGN KEY (educational_program_element_id)
        REFERENCES educational_program_elements (Id) ON DELETE CASCADE,
    CONSTRAINT fk_comm_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS educational_program_managers (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    educational_program_id INT NOT NULL,
    user_id INT NOT NULL,
    assigned_by_user_id INT NULL,
    assigned_at DATETIME(6) NULL,
    CONSTRAINT fk_mgr_prog FOREIGN KEY (educational_program_id)
        REFERENCES educational_programs (Id) ON DELETE CASCADE,
    CONSTRAINT fk_mgr_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT fk_mgr_by FOREIGN KEY (assigned_by_user_id) REFERENCES users (Id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS educational_program_assignments (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    educational_program_id INT NOT NULL,
    department_id INT NOT NULL,
    faculty_id INT NOT NULL,
    CONSTRAINT fk_epa_prog FOREIGN KEY (educational_program_id)
        REFERENCES educational_programs (Id) ON DELETE CASCADE,
    CONSTRAINT fk_epa_dept FOREIGN KEY (department_id) REFERENCES departments (Id) ON DELETE CASCADE,
    CONSTRAINT fk_epa_fac FOREIGN KEY (faculty_id) REFERENCES facultys (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS approver_assignments (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    approver_user_id INT NOT NULL,
    faculty_id INT NULL,
    department_id INT NULL,
    assigned_by_user_id INT NOT NULL,
    assigned_at DATETIME(6) NOT NULL,
    CONSTRAINT fk_appr_user FOREIGN KEY (approver_user_id) REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT fk_appr_by FOREIGN KEY (assigned_by_user_id) REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT fk_appr_fac FOREIGN KEY (faculty_id) REFERENCES facultys (Id) ON DELETE CASCADE,
    CONSTRAINT fk_appr_dept FOREIGN KEY (department_id) REFERENCES departments (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS element_status_history (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    educational_program_element_id INT NOT NULL,
    user_id INT NOT NULL,
    old_status LONGTEXT NOT NULL,
    new_status LONGTEXT NOT NULL,
    change_date DATETIME(6) NOT NULL,
    comment LONGTEXT NOT NULL,
    file_path LONGTEXT NULL,
    file_name LONGTEXT NULL,
    CONSTRAINT fk_hist_elem FOREIGN KEY (educational_program_element_id)
        REFERENCES educational_program_elements (Id) ON DELETE CASCADE,
    CONSTRAINT fk_hist_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS notifications (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    educational_program_element_id INT NOT NULL,
    actor_name LONGTEXT NOT NULL,
    type LONGTEXT NOT NULL,
    title LONGTEXT NOT NULL,
    message LONGTEXT NOT NULL,
    created_at DATETIME(6) NOT NULL,
    is_read TINYINT(1) NOT NULL DEFAULT 0,
    read_at DATETIME(6) NULL,
    CONSTRAINT fk_notif_user FOREIGN KEY (user_id) REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT fk_notif_elem FOREIGN KEY (educational_program_element_id)
        REFERENCES educational_program_elements (Id) ON DELETE CASCADE,
    INDEX ix_notif_user (user_id),
    INDEX ix_notif_elem (educational_program_element_id),
    INDEX ix_notif_unread (user_id, is_read)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` VARCHAR(150) NOT NULL,
    `ProductVersion` VARCHAR(32) NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB;
