SET NAMES utf8mb4;
USE `personal_cabinet`;

START TRANSACTION;

-- Актуальный снимок данных сайта на 18 июня 2026 года.
-- Скрипт можно запускать повторно: записи с теми же Id будут обновлены.

INSERT INTO `roles` (`Id`, `Name`, `Description`) VALUES
(1, 'Manager', 'Руководитель ОПОП'),
(2, 'Approver', 'Согласующий'),
(3, 'Moderator', 'Модератор'),
(4, 'Admin', 'Администратор')
ON DUPLICATE KEY UPDATE
    `Name` = VALUES(`Name`),
    `Description` = VALUES(`Description`);

INSERT INTO `users`
    (`Id`, `username`, `password_hash`, `full_name`, `role_id`, `post`, `approval_status`, `rejection_reason`)
VALUES
(1, 'manager', '866485796cfa8d7c0cf7111640205b83076433547577511d81f8030ae99ecea5', 'Иванов Иван Иванович', 1, 'Заведующий кафедрой', 'Approved', NULL),
(2, 'approver', '1c391319644c0c6e9f5955e44e55862a8fd27b3b9d9863456500096ccf512db3', 'Петрова Анна Сергеевна', 2, 'Декан факультета', 'Approved', NULL),
(3, 'moderator', '4c8425b174053ea6935b29c2b0e0aa4e2eab1a01b784e6ac91b8bdce9c26235a', 'Сидоров Петр Алексеевич', 3, 'Модератор', 'Approved', NULL),
(4, 'admin', '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9', 'Козлова Мария Ивановна', 4, 'Администратор', 'Approved', NULL),
(5, 'amir', '4d22be2786c0540b0389fa978a6117cbe9849c2e47e0abbf87ae88f0974c3f27', 'Амир', 1, 'Ректор', 'Approved', NULL)
ON DUPLICATE KEY UPDATE
    `username` = VALUES(`username`),
    `password_hash` = VALUES(`password_hash`),
    `full_name` = VALUES(`full_name`),
    `role_id` = VALUES(`role_id`),
    `post` = VALUES(`post`),
    `approval_status` = VALUES(`approval_status`),
    `rejection_reason` = VALUES(`rejection_reason`);

UPDATE `roles`
SET `normalized_name` = UPPER(`Name`),
    `concurrency_stamp` = COALESCE(`concurrency_stamp`, UUID());

UPDATE `users`
SET `normalized_username` = UPPER(`username`),
    `security_stamp` = COALESCE(`security_stamp`, UUID()),
    `concurrency_stamp` = COALESCE(`concurrency_stamp`, UUID());

INSERT INTO `user_roles` (`user_id`, `role_id`)
SELECT `Id`, `role_id`
FROM `users`
WHERE `role_id` IS NOT NULL
ON DUPLICATE KEY UPDATE `role_id` = VALUES(`role_id`);

INSERT INTO `facultys` (`Id`, `Name`) VALUES
(1, 'Факультет информационных технологий'),
(2, 'Факультет математики и механики'),
(3, 'Факультет педагогического образования')
ON DUPLICATE KEY UPDATE
    `Name` = VALUES(`Name`);

INSERT INTO `departments` (`Id`, `code_department`, `Name`) VALUES
(1, 'Каф.ПМИ', 'Кафедра прикладной математики и информатики'),
(2, 'Каф.ИВТ', 'Кафедра информационных вычислительных технологий'),
(3, 'Каф.МАТЕМ', 'Кафедра математического анализа')
ON DUPLICATE KEY UPDATE
    `code_department` = VALUES(`code_department`),
    `Name` = VALUES(`Name`);

INSERT INTO `educational_programs`
    (`Id`, `code_referral`, `Name`, `educational_level`, `year_approvals`, `Status`, `user_id`)
VALUES
(1, '01.03.02', 'Прикладная математика и информатика', 'Бакалавриат', NULL, 'Разрабатывается', 5),
(2, '09.03.01', 'Информатика и вычислительная техника', 'Бакалавриат', NULL, 'Разрабатывается', 1),
(3, '44.03.05', 'Педагогическое образование (Математика. Информатика)', 'Бакалавриат', NULL, 'Разрабатывается', 1)
ON DUPLICATE KEY UPDATE
    `code_referral` = VALUES(`code_referral`),
    `Name` = VALUES(`Name`),
    `educational_level` = VALUES(`educational_level`),
    `year_approvals` = VALUES(`year_approvals`),
    `Status` = VALUES(`Status`),
    `user_id` = VALUES(`user_id`);

INSERT INTO `educational_program_managers`
    (`Id`, `educational_program_id`, `user_id`, `assigned_by_user_id`, `assigned_at`)
VALUES
(3, 3, 1, 4, '2026-06-17 18:05:13'),
(10, 2, 1, 4, '2026-06-17 18:05:13'),
(11, 1, 5, 4, '2026-06-17 18:05:13')
ON DUPLICATE KEY UPDATE
    `educational_program_id` = VALUES(`educational_program_id`),
    `user_id` = VALUES(`user_id`),
    `assigned_by_user_id` = VALUES(`assigned_by_user_id`),
    `assigned_at` = VALUES(`assigned_at`);

INSERT INTO `educational_program_assignments`
    (`Id`, `educational_program_id`, `department_id`, `faculty_id`)
VALUES
(1, 1, 1, 1),
(2, 2, 2, 1),
(3, 3, 1, 3)
ON DUPLICATE KEY UPDATE
    `educational_program_id` = VALUES(`educational_program_id`),
    `department_id` = VALUES(`department_id`),
    `faculty_id` = VALUES(`faculty_id`);

INSERT INTO `approver_assignments`
    (`Id`, `approver_user_id`, `faculty_id`, `department_id`, `assigned_by_user_id`, `assigned_at`)
VALUES
(1, 2, 1, NULL, 4, '2026-06-17 15:55:01.108948'),
(2, 2, 2, NULL, 4, '2026-06-17 17:52:05.115869')
ON DUPLICATE KEY UPDATE
    `approver_user_id` = VALUES(`approver_user_id`),
    `faculty_id` = VALUES(`faculty_id`),
    `department_id` = VALUES(`department_id`),
    `assigned_by_user_id` = VALUES(`assigned_by_user_id`),
    `assigned_at` = VALUES(`assigned_at`);

INSERT INTO `educational_program_elements`
    (`Id`, `educational_program_id`, `type_element`, `Name`, `upload_date`, `Description`, `status_approvals`, `file_path`, `file_name`)
VALUES
(1, 1, 'Main', 'Учебный план (очный)', '2026-06-18', 'Основной учебный план', 'Согласовано', '5f1ba6b9-ac4d-4045-b956-d62c361ed4c0_toll_ticket (4).pdf', 'toll_ticket (4).pdf'),
(2, 1, 'Main', 'Пояснительная записка', '2026-06-17', 'Общая информация', 'Согласовано', 'd4d01c6a-34bc-4ed4-813a-abcb12637cb8_toll_ticket (3) (1).pdf', 'toll_ticket (3) (1).pdf'),
(3, 1, 'Main', 'Календарный учебный график', '2026-06-17', 'График обучения', 'На доработку', 'd8564ab6-82df-4fd8-bfe1-70ce8ef9efbd_toll_ticket (3).pdf', 'toll_ticket (3).pdf'),
(4, 1, 'Main', 'Программа воспитательной работы', NULL, 'Воспитательная программа', '', NULL, NULL),
(5, 1, 'Main', 'Календарный план воспитательной работы', NULL, 'Календарный план', '', NULL, NULL),
(6, 1, 'Discipline', 'Философия', NULL, 'Б1.О.01', 'Согласовано', NULL, NULL),
(7, 1, 'Discipline', 'Математический анализ', NULL, 'Б1.О.02', 'Согласовано', NULL, NULL),
(8, 1, 'Discipline', 'Линейная алгебра', NULL, 'Б1.О.03', '', NULL, NULL),
(9, 1, 'Discipline', 'Программирование', NULL, 'Б1.О.04', 'Согласовано', NULL, NULL),
(10, 1, 'Discipline', 'Базы данных', NULL, 'Б1.О.05', '', NULL, NULL),
(11, 1, 'Practice', 'Учебная практика', NULL, 'Практика 1', '', NULL, NULL),
(12, 1, 'Practice', 'Производственная практика', NULL, 'Практика 2', '', NULL, NULL),
(13, 1, 'GIA', 'Государственный экзамен', NULL, 'ГИА', '', NULL, NULL),
(14, 1, 'GIA', 'Выпускная квалификационная работа', NULL, 'ВКР', '', NULL, NULL)
ON DUPLICATE KEY UPDATE
    `educational_program_id` = VALUES(`educational_program_id`),
    `type_element` = VALUES(`type_element`),
    `Name` = VALUES(`Name`),
    `upload_date` = VALUES(`upload_date`),
    `Description` = VALUES(`Description`),
    `status_approvals` = VALUES(`status_approvals`),
    `file_path` = VALUES(`file_path`),
    `file_name` = VALUES(`file_name`);

INSERT INTO `comments_educational_program_element`
    (`Id`, `educational_program_element_id`, `user_id`, `date_time_comment`, `comment_content`, `Status`)
VALUES
(1, 6, 1, '2026-06-17 13:37:52.558073', 'отлично', 'Новый'),
(2, 3, 5, '2026-06-17 16:53:20.062473', 'asdasd', 'Новый'),
(3, 2, 5, '2026-06-17 16:54:09.608812', '123qqwe', 'Прочитан'),
(4, 2, 4, '2026-06-17 16:54:30.766171', 'asdasd', 'Прочитан')
ON DUPLICATE KEY UPDATE
    `educational_program_element_id` = VALUES(`educational_program_element_id`),
    `user_id` = VALUES(`user_id`),
    `date_time_comment` = VALUES(`date_time_comment`),
    `comment_content` = VALUES(`comment_content`),
    `Status` = VALUES(`Status`);

INSERT INTO `element_status_history`
    (`Id`, `educational_program_element_id`, `user_id`, `old_status`, `new_status`, `change_date`, `comment`)
VALUES
(1, 2, 1, 'На доработку', 'Загружено', '2026-06-01 23:57:02.550537', 'Загружен файл: toll_ticket (8).pdf'),
(2, 2, 1, 'Загружено', 'Загружено', '2026-06-01 23:57:33.223012', 'Загружен файл: toll_ticket (8).pdf'),
(3, 7, 2, 'На рассмотрении', 'Согласовано', '2026-06-17 13:37:34.542442', ' всё отлично'),
(4, 6, 3, 'Согласовано', 'Опубликовано на сайте', '2026-06-17 15:59:18.811456', 'Опубликовано на сайте'),
(5, 6, 3, 'Опубликовано на сайте', 'Согласовано', '2026-06-17 15:59:29.936992', 'Снято с публикации'),
(6, 3, 5, '', 'Загружено', '2026-06-17 16:04:43.351581', 'Загружен файл: toll_ticket (3).pdf'),
(7, 3, 2, 'Загружено', 'Согласовано', '2026-06-17 16:05:24.219733', 'Согласовано'),
(8, 2, 2, 'Загружено', 'На рассмотрении', '2026-06-17 16:06:13.967265', 'Отправлено на рассмотрение'),
(9, 3, 4, 'Согласовано', 'На доработку', '2026-06-17 16:16:30.803283', 'Отправлено на доработку администратором'),
(10, 2, 5, 'На рассмотрении', 'Загружено', '2026-06-17 16:54:03.290188', 'Загружен файл: toll_ticket (3) (1).pdf'),
(11, 2, 4, 'Загружено', 'Согласовано', '2026-06-17 20:44:00.460910', 'Согласовано'),
(12, 1, 5, '', 'Загружено', '2026-06-18 18:22:13.304742', 'Загружен файл: toll_ticket (4).pdf'),
(13, 1, 2, 'Загружено', 'Согласовано', '2026-06-18 18:22:26.317000', 'asdas'),
(14, 1, 3, 'Согласовано', 'Опубликовано на сайте', '2026-06-18 18:28:02.967471', 'отлично'),
(15, 1, 3, 'Опубликовано на сайте', 'Согласовано', '2026-06-18 18:28:09.145229', 'плохо')
ON DUPLICATE KEY UPDATE
    `educational_program_element_id` = VALUES(`educational_program_element_id`),
    `user_id` = VALUES(`user_id`),
    `old_status` = VALUES(`old_status`),
    `new_status` = VALUES(`new_status`),
    `change_date` = VALUES(`change_date`),
    `comment` = VALUES(`comment`);

COMMIT;
