IF OBJECT_ID('EmployeeLeave', 'U') IS NULL
BEGIN
    CREATE TABLE [EmployeeLeave]
    (
        [EmployeeLeaveId] NVARCHAR(50) NOT NULL CONSTRAINT PK_EmployeeLeave PRIMARY KEY,
        [EmployeeId] VARCHAR(50) NOT NULL,
        [StartDate] DATE NOT NULL,
        [EndDate] DATE NOT NULL,
        [LeaveType] NVARCHAR(50) NOT NULL CONSTRAINT DF_EmployeeLeave_LeaveType DEFAULT 'Vacation',
        [Reason] NVARCHAR(250) NULL,
        [ReplacementEmployeeId] VARCHAR(50) NULL,
        CONSTRAINT FK_EmployeeLeave_Employee FOREIGN KEY ([EmployeeId]) REFERENCES [Employee]([EmployeeId]) ON DELETE CASCADE,
        CONSTRAINT FK_EmployeeLeave_ReplacementEmployee FOREIGN KEY ([ReplacementEmployeeId]) REFERENCES [Employee]([EmployeeId])
    );
END;
GO
