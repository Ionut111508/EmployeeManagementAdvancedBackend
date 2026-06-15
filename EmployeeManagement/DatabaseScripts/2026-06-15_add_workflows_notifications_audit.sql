DECLARE @NeedsWorkflowBackfill bit = CASE WHEN OBJECT_ID('AuditLog', 'U') IS NULL THEN 1 ELSE 0 END;

IF COL_LENGTH('TaskItem', 'Status') IS NULL
    ALTER TABLE TaskItem ADD Status nvarchar(30) NOT NULL CONSTRAINT DF_TaskItem_Status DEFAULT 'Backlog';
IF @NeedsWorkflowBackfill = 1
    EXEC('UPDATE TaskItem SET Status = CASE WHEN PlannedEndDate < CAST(GETDATE() AS date) THEN ''Completed'' WHEN PlannedStartDate <= CAST(GETDATE() AS date) AND PlannedEndDate >= CAST(GETDATE() AS date) THEN ''InProgress'' WHEN PlannedStartDate > CAST(GETDATE() AS date) THEN ''Ready'' ELSE ''Backlog'' END');

IF COL_LENGTH('Timesheet', 'Status') IS NULL ALTER TABLE Timesheet ADD Status nvarchar(20) NOT NULL CONSTRAINT DF_Timesheet_Status DEFAULT 'Approved';
IF COL_LENGTH('Timesheet', 'SubmittedAt') IS NULL ALTER TABLE Timesheet ADD SubmittedAt datetime2 NOT NULL CONSTRAINT DF_Timesheet_SubmittedAt DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Timesheet', 'ReviewedAt') IS NULL ALTER TABLE Timesheet ADD ReviewedAt datetime2 NULL;
IF COL_LENGTH('Timesheet', 'ReviewedByEmployeeId') IS NULL ALTER TABLE Timesheet ADD ReviewedByEmployeeId nvarchar(50) NULL;
IF COL_LENGTH('Timesheet', 'ReviewComment') IS NULL ALTER TABLE Timesheet ADD ReviewComment nvarchar(500) NULL;

IF OBJECT_ID('AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE AuditLog (
        AuditLogId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CreatedAt datetime2 NOT NULL,
        ActorEmployeeId nvarchar(50) NULL,
        ActorRole nvarchar(30) NOT NULL,
        Action nvarchar(50) NOT NULL,
        EntityType nvarchar(50) NOT NULL,
        EntityId nvarchar(200) NOT NULL,
        ProjectId nvarchar(50) NULL,
        Summary nvarchar(500) NOT NULL,
        BeforeJson nvarchar(max) NULL,
        AfterJson nvarchar(max) NULL
    );
END;
