CREATE DATABASE IF NOT EXISTS personal_cabinet;
USE personal_cabinet;

-- Таблица ролей
CREATE TABLE IF NOT EXISTS roles (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name LONGTEXT NOT NULL,
    Description LONGTEXT NOT NULL
) ENGINE=InnoDB;

-- Таблица пользователей
CREATE TABLE IF NOT EXISTS users (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(100) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name LONGTEXT NOT NULL,
    link_role LONGTEXT NOT NULL,
    post LONGTEXT NOT NULL,
    approval_status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    rejection_reason LONGTEXT NULL,
    CONSTRAINT UQ_users_username UNIQUE (username)
) ENGINE=InnoDB;

-- Таблица факультетов
CREATE TABLE IF NOT EXISTS facultys (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name LONGTEXT NOT NULL
) ENGINE=InnoDB;

-- Таблица кафедр
CREATE TABLE IF NOT EXISTS departments (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    code_department LONGTEXT NULL,
    Name LONGTEXT NOT NULL
) ENGINE=InnoDB;

-- Таблица образовательных программ (ОПОП)
CREATE TABLE IF NOT EXISTS educational_programs (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    code_referral LONGTEXT NOT NULL,
    Name LONGTEXT NOT NULL,
    educational_level LONGTEXT NOT NULL,
    year_approvals INT NULL,
    Status LONGTEXT NOT NULL,
    user_id INT NOT NULL,
    CONSTRAINT FK_educational_programs_users_user_id FOREIGN KEY (user_id)
        REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Таблица элементов образовательной программы
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
    CONSTRAINT FK_educational_program_elements_educational_programs FOREIGN KEY (educational_program_id)
        REFERENCES educational_programs (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Таблица комментариев к элементам ОПОП
CREATE TABLE IF NOT EXISTS comments_educational_program_element (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    educational_program_element_id INT NOT NULL,
    user_id INT NOT NULL,
    date_time_comment DATETIME(6) NOT NULL,
    comment_content LONGTEXT NOT NULL,
    Status LONGTEXT NOT NULL,
    CONSTRAINT FK_comments_element FOREIGN KEY (educational_program_element_id)
        REFERENCES educational_program_elements (Id) ON DELETE CASCADE,
    CONSTRAINT FK_comments_user FOREIGN KEY (user_id)
        REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Таблица руководителей ОПОП
CREATE TABLE IF NOT EXISTS educational_program_managers (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    educational_program_id INT NOT NULL,
    user_id INT NOT NULL,
    CONSTRAINT FK_managers_educational_program FOREIGN KEY (educational_program_id)
        REFERENCES educational_programs (Id) ON DELETE CASCADE,
    CONSTRAINT FK_managers_user FOREIGN KEY (user_id)
        REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Таблица закрепления ОПОП за кафедрами и факультетами
CREATE TABLE IF NOT EXISTS educational_program_assignments (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    educational_program_id INT NOT NULL,
    department_id INT NOT NULL,
    faculty_id INT NOT NULL,
    CONSTRAINT FK_assignments_educational_program FOREIGN KEY (educational_program_id)
        REFERENCES educational_programs (Id) ON DELETE CASCADE,
    CONSTRAINT FK_assignments_department FOREIGN KEY (department_id)
        REFERENCES departments (Id) ON DELETE CASCADE,
    CONSTRAINT FK_assignments_faculty FOREIGN KEY (faculty_id)
        REFERENCES facultys (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Таблица назначений согласующих
CREATE TABLE IF NOT EXISTS approver_assignments (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    approver_user_id INT NOT NULL,
    faculty_id INT NULL,
    department_id INT NULL,
    assigned_by_user_id INT NOT NULL,
    assigned_at DATETIME(6) NOT NULL,
    CONSTRAINT FK_approver_assignments_user FOREIGN KEY (approver_user_id)
        REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_approver_assignments_assigned_by_user FOREIGN KEY (assigned_by_user_id)
        REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_approver_assignments_faculty FOREIGN KEY (faculty_id)
        REFERENCES facultys (Id) ON DELETE CASCADE,
    CONSTRAINT FK_approver_assignments_department FOREIGN KEY (department_id)
        REFERENCES departments (Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- Таблица истории изменений статусов элементов
CREATE TABLE IF NOT EXISTS element_status_history (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    educational_program_element_id INT NOT NULL,
    user_id INT NOT NULL,
    old_status LONGTEXT NOT NULL,
    new_status LONGTEXT NOT NULL,
    change_date DATETIME(6) NOT NULL,
    comment LONGTEXT NOT NULL,
    CONSTRAINT FK_history_element FOREIGN KEY (educational_program_element_id)
        REFERENCES educational_program_elements (Id) ON DELETE CASCADE,
    CONSTRAINT FK_history_user FOREIGN KEY (user_id)
        REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB;
