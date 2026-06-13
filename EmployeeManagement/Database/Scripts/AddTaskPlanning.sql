IF COL_LENGTH('TaskItem', 'PlannedStartDate') IS NULL
    ALTER TABLE TaskItem ADD PlannedStartDate date NULL;
GO

IF COL_LENGTH('TaskItem', 'PlannedEndDate') IS NULL
    ALTER TABLE TaskItem ADD PlannedEndDate date NULL;
GO

IF COL_LENGTH('Allocation', 'HoursPerDay') IS NOT NULL
    ALTER TABLE Allocation ALTER COLUMN HoursPerDay decimal(4,2) NOT NULL;
GO

UPDATE task
SET PlannedStartDate = dates.MinStartDate,
    PlannedEndDate = dates.MaxEndDate
FROM TaskItem task
INNER JOIN (
    SELECT ProjectId, TaskId, MIN(AllocationStartDate) AS MinStartDate,
           MAX(COALESCE(AllocationEndDate, AllocationStartDate)) AS MaxEndDate
    FROM Allocation
    GROUP BY ProjectId, TaskId
) dates ON dates.ProjectId = task.ProjectId AND dates.TaskId = task.TaskId
WHERE task.PlannedStartDate IS NULL OR task.PlannedEndDate IS NULL;
GO
