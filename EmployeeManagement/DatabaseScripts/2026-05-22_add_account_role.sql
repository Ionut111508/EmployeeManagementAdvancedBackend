IF COL_LENGTH('Account', 'Role') IS NULL
BEGIN
    ALTER TABLE [Account] ADD [Role] NVARCHAR(30) NOT NULL CONSTRAINT DF_Account_Role DEFAULT 'Employee';
END;
GO

UPDATE [Account]
SET [Role] = 'Admin'
WHERE [AccountId] = 'ACC001'
   OR LOWER([Username]) LIKE '%admin%';
GO

UPDATE a
SET a.[Role] = 'Manager'
FROM [Account] a
JOIN [Employee] e ON e.[AccountId] = a.[AccountId]
WHERE a.[Role] <> 'Admin'
  AND EXISTS (
      SELECT 1
      FROM [ProjectManager] pm
      WHERE pm.[EmployeeId] = e.[EmployeeId]
  );
GO

UPDATE [Account]
SET [Role] = 'Employee'
WHERE [Role] IS NULL
   OR [Role] NOT IN ('Admin', 'Manager', 'Employee');
GO

IF OBJECT_ID('CK_Account_Role', 'C') IS NULL
BEGIN
    ALTER TABLE [Account]
    ADD CONSTRAINT CK_Account_Role CHECK ([Role] IN ('Admin', 'Manager', 'Employee'));
END;
GO
