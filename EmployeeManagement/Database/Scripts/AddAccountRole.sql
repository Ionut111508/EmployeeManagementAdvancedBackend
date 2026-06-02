-- Add and normalize Account.Role for role-based application access.
-- Run this script manually on SQL Server before using role-aware screens.

IF COL_LENGTH('Account', 'Role') IS NULL
BEGIN
    ALTER TABLE [Account]
    ADD [Role] NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Account_Role DEFAULT 'Employee';
END;
GO

UPDATE [Account]
SET [Role] = 'Admin'
WHERE LOWER([Username]) = 'admin';
GO

UPDATE [Account]
SET [Role] = 'Employee'
WHERE [Role] IS NULL OR [Role] NOT IN ('Admin', 'Manager', 'Employee');
GO

IF OBJECT_ID('CK_Account_Role', 'C') IS NULL
BEGIN
    ALTER TABLE [Account]
    ADD CONSTRAINT CK_Account_Role CHECK ([Role] IN ('Admin', 'Manager', 'Employee'));
END;
GO
