-- Create EmployeeLeave table if it does not exist
-- This script is idempotent and can be run multiple times safely

IF OBJECT_ID('EmployeeLeave', 'U') IS NULL
BEGIN
    CREATE TABLE EmployeeLeave (
        EmployeeLeaveId NVARCHAR(50) NOT NULL PRIMARY KEY,
        EmployeeId VARCHAR(50) NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        LeaveType NVARCHAR(50) NOT NULL,
        Reason NVARCHAR(500) NULL,
        ReplacementEmployeeId VARCHAR(50) NULL,
        CONSTRAINT FK_EmployeeLeave_Employee FOREIGN KEY (EmployeeId)
            REFERENCES Employee(EmployeeId),
        CONSTRAINT FK_EmployeeLeave_ReplacementEmployee FOREIGN KEY (ReplacementEmployeeId)
            REFERENCES Employee(EmployeeId)
    );

    -- Create index for faster queries on employee and date range
    CREATE INDEX IX_EmployeeLeave_EmployeeId_StartDate_EndDate
    ON EmployeeLeave(EmployeeId, StartDate, EndDate);

    PRINT 'EmployeeLeave table created successfully.';
END
ELSE
BEGIN
    PRINT 'EmployeeLeave table already exists.';
END
