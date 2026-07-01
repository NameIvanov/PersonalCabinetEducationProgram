SET NAMES utf8mb4;
USE `personal_cabinet`;

START TRANSACTION;

-- init.sql already creates the current ASP.NET Identity schema.
-- This step safely finalizes Identity data and tells EF Core that the
-- equivalent migrations are already represented in the SQL-created schema.

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

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` VARCHAR(150) NOT NULL,
    `ProductVersion` VARCHAR(32) NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES
('20260619132300_InitialCreate', '8.0.0'),
('20260619134645_AddHistoryFileVersions', '8.0.0'),
('20260619150134_AddNotifications', '8.0.0')
ON DUPLICATE KEY UPDATE
    `ProductVersion` = VALUES(`ProductVersion`);

COMMIT;
