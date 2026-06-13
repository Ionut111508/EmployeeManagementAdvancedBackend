SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Dedicated managers for the Health and Retail projects.
IF NOT EXISTS (SELECT 1 FROM Account WHERE AccountId = 'ACC011')
    INSERT INTO Account (AccountId, Username, PasswordHash, Role)
    VALUES ('ACC011', 'sorin.petrescu', 'PBKDF2$120000$y72syuDz6UgeAnuwQ8miAg==$4mL7Lg6D1AotzAkJXMNJtKVSSYMHZ+kk6DxkZaajBGA=', 'Manager');
ELSE
    UPDATE Account SET Username = 'sorin.petrescu', Role = 'Manager' WHERE AccountId = 'ACC011';

IF NOT EXISTS (SELECT 1 FROM Account WHERE AccountId = 'ACC012')
    INSERT INTO Account (AccountId, Username, PasswordHash, Role)
    VALUES ('ACC012', 'laura.munteanu', 'PBKDF2$120000$09aPfX8Ku7k8YBHvCmkf7Q==$qWT0zEz0+fdDY1pHKvyiU5JgXo5VTZP82cIsSSKHjCw=', 'Manager');
ELSE
    UPDATE Account SET Username = 'laura.munteanu', Role = 'Manager' WHERE AccountId = 'ACC012';

IF NOT EXISTS (SELECT 1 FROM Employee WHERE EmployeeId = 'E011')
    INSERT INTO Employee (EmployeeId, FirstName, LastName, Email, PhoneNumber, AccountId, WorkNormId)
    VALUES ('E011', 'Sorin', 'Petrescu', 'sorin.petrescu@novatech.local', '0700000011', 'ACC011', 'WN_FULL');

IF NOT EXISTS (SELECT 1 FROM Employee WHERE EmployeeId = 'E012')
    INSERT INTO Employee (EmployeeId, FirstName, LastName, Email, PhoneNumber, AccountId, WorkNormId)
    VALUES ('E012', 'Laura', 'Munteanu', 'laura.munteanu@novatech.local', '0700000012', 'ACC012', 'WN_FULL');

IF NOT EXISTS (SELECT 1 FROM EmployeeDepartment WHERE EmployeeId = 'E011' AND DepartmentId = 'DEP_PM')
    INSERT INTO EmployeeDepartment (EmployeeId, DepartmentId, StartDate, EndDate)
    VALUES ('E011', 'DEP_PM', '2026-05-01', NULL);

IF NOT EXISTS (SELECT 1 FROM EmployeeDepartment WHERE EmployeeId = 'E012' AND DepartmentId = 'DEP_PM')
    INSERT INTO EmployeeDepartment (EmployeeId, DepartmentId, StartDate, EndDate)
    VALUES ('E012', 'DEP_PM', '2026-05-01', NULL);

IF NOT EXISTS (SELECT 1 FROM EmployeeSkill WHERE EmployeeId = 'E011' AND SkillId = 'SK_PM_SENIOR')
    INSERT INTO EmployeeSkill (EmployeeId, SkillId, AcquiredDate) VALUES ('E011', 'SK_PM_SENIOR', '2024-01-01');

IF NOT EXISTS (SELECT 1 FROM EmployeeSkill WHERE EmployeeId = 'E012' AND SkillId = 'SK_PM_SENIOR')
    INSERT INTO EmployeeSkill (EmployeeId, SkillId, AcquiredDate) VALUES ('E012', 'SK_PM_SENIOR', '2023-06-01');

DELETE FROM ProjectManager WHERE ProjectId IN ('PRJ_BANK', 'PRJ_HEALTH', 'PRJ_RETAIL');
INSERT INTO ProjectManager (EmployeeId, ProjectId, StartDate, EndDate) VALUES
    ('E005', 'PRJ_BANK', '2026-05-01', NULL),
    ('E011', 'PRJ_HEALTH', '2026-05-01', NULL),
    ('E012', 'PRJ_RETAIL', '2026-05-01', NULL);

-- Skills now describe the actual work represented by each task.
UPDATE TaskItem SET RequiredSkillId = 'SK_DOTNET_SENIOR' WHERE ProjectId = 'PRJ_BANK' AND TaskId = 'BANK_T01';
UPDATE TaskItem SET RequiredSkillId = 'SK_CSHARP_MID' WHERE ProjectId = 'PRJ_BANK' AND TaskId = 'BANK_T02';
UPDATE TaskItem SET RequiredSkillId = 'SK_REACT_MID' WHERE ProjectId = 'PRJ_BANK' AND TaskId = 'BANK_T03';
UPDATE TaskItem SET RequiredSkillId = 'SK_AUTOMATION_MID' WHERE ProjectId = 'PRJ_BANK' AND TaskId = 'BANK_T04';
UPDATE TaskItem SET RequiredSkillId = 'SK_DOTNET_SENIOR' WHERE ProjectId = 'PRJ_HEALTH' AND TaskId = 'HEALTH_T01';
UPDATE TaskItem SET RequiredSkillId = 'SK_BA_MID' WHERE ProjectId = 'PRJ_HEALTH' AND TaskId = 'HEALTH_T02';
UPDATE TaskItem SET RequiredSkillId = 'SK_REACT_MID' WHERE ProjectId = 'PRJ_HEALTH' AND TaskId = 'HEALTH_T03';
UPDATE TaskItem SET RequiredSkillId = 'SK_AZURE_MID' WHERE ProjectId = 'PRJ_HEALTH' AND TaskId = 'HEALTH_T04';
UPDATE TaskItem SET RequiredSkillId = 'SK_REACT_MID' WHERE ProjectId = 'PRJ_RETAIL' AND TaskId = 'RETAIL_T01';
UPDATE TaskItem SET RequiredSkillId = 'SK_REACT_MID' WHERE ProjectId = 'PRJ_RETAIL' AND TaskId = 'RETAIL_T02';
UPDATE TaskItem SET RequiredSkillId = 'SK_REACT_MID' WHERE ProjectId = 'PRJ_RETAIL' AND TaskId = 'RETAIL_T03';
UPDATE TaskItem SET RequiredSkillId = 'SK_TESTING_MID' WHERE ProjectId = 'PRJ_RETAIL' AND TaskId = 'RETAIL_T04';

-- Work completed before today uses its real delivery date, not the old placeholder deadline.
UPDATE TaskItem SET PlannedStartDate = '2026-05-20', PlannedEndDate = '2026-06-02' WHERE ProjectId = 'PRJ_BANK' AND TaskId = 'BANK_T01';
UPDATE TaskItem SET PlannedStartDate = '2026-05-22', PlannedEndDate = '2026-06-10' WHERE ProjectId = 'PRJ_BANK' AND TaskId = 'BANK_T02';
UPDATE TaskItem SET PlannedStartDate = '2026-05-25', PlannedEndDate = '2026-06-05' WHERE ProjectId = 'PRJ_BANK' AND TaskId = 'BANK_T03';
UPDATE TaskItem SET PlannedStartDate = '2026-06-01', PlannedEndDate = '2026-06-12' WHERE ProjectId = 'PRJ_BANK' AND TaskId = 'BANK_T04';
UPDATE TaskItem SET PlannedStartDate = '2026-05-27', PlannedEndDate = '2026-06-09' WHERE ProjectId = 'PRJ_RETAIL' AND TaskId = 'RETAIL_T01';

DECLARE @DemoTasks TABLE (ProjectId varchar(50), TaskId varchar(50));
INSERT INTO @DemoTasks VALUES
    ('PRJ_BANK', 'BANK_T01'), ('PRJ_BANK', 'BANK_T02'), ('PRJ_BANK', 'BANK_T03'), ('PRJ_BANK', 'BANK_T04'),
    ('PRJ_HEALTH', 'HEALTH_T01'), ('PRJ_HEALTH', 'HEALTH_T02'), ('PRJ_HEALTH', 'HEALTH_T03'), ('PRJ_HEALTH', 'HEALTH_T04'),
    ('PRJ_RETAIL', 'RETAIL_T01'), ('PRJ_RETAIL', 'RETAIL_T02'), ('PRJ_RETAIL', 'RETAIL_T03'), ('PRJ_RETAIL', 'RETAIL_T04');

DELETE timesheet
FROM Timesheet timesheet
INNER JOIN @DemoTasks task ON task.ProjectId = timesheet.ProjectId AND task.TaskId = timesheet.TaskId;

DELETE allocation
FROM Allocation allocation
INNER JOIN @DemoTasks task ON task.ProjectId = allocation.ProjectId AND task.TaskId = allocation.TaskId;

-- Every row uses a uniform daily load over its interval. Combined totals match task estimates exactly.
INSERT INTO Allocation (EmployeeId, ProjectId, TaskId, AllocationStartDate, AllocationEndDate, HoursPerDay) VALUES
    ('E001', 'PRJ_BANK', 'BANK_T01', '2026-05-20', '2026-06-02', 8.00),
    ('E006', 'PRJ_BANK', 'BANK_T02', '2026-05-22', '2026-06-10', 5.00),
    ('E002', 'PRJ_BANK', 'BANK_T03', '2026-05-25', '2026-06-05', 6.00),
    ('E003', 'PRJ_BANK', 'BANK_T04', '2026-06-01', '2026-06-12', 5.00),
    ('E001', 'PRJ_HEALTH', 'HEALTH_T01', '2026-06-08', '2026-06-24', 5.00),
    ('E007', 'PRJ_HEALTH', 'HEALTH_T02', '2026-06-01', '2026-06-26', 4.50),
    ('E002', 'PRJ_HEALTH', 'HEALTH_T03', '2026-06-15', '2026-07-02', 5.00),
    ('E004', 'PRJ_HEALTH', 'HEALTH_T04', '2026-06-10', '2026-06-22', 5.00),
    ('E008', 'PRJ_RETAIL', 'RETAIL_T01', '2026-05-27', '2026-06-09', 7.50),
    ('E006', 'PRJ_RETAIL', 'RETAIL_T02', '2026-06-17', '2026-07-10', 5.00),
    ('E008', 'PRJ_RETAIL', 'RETAIL_T02', '2026-07-06', '2026-07-10', 1.00),
    ('E008', 'PRJ_RETAIL', 'RETAIL_T03', '2026-06-22', '2026-07-03', 6.00),
    ('E009', 'PRJ_RETAIL', 'RETAIL_T04', '2026-06-22', '2026-07-03', 5.00),
    ('E003', 'PRJ_RETAIL', 'RETAIL_T04', '2026-07-06', '2026-07-06', 5.00);

CREATE TABLE #Calendar (WorkDate date NOT NULL PRIMARY KEY);
DECLARE @CalendarDate date = '2026-05-01';
WHILE @CalendarDate <= '2026-07-31'
BEGIN
    INSERT INTO #Calendar (WorkDate) VALUES (@CalendarDate);
    SET @CalendarDate = DATEADD(day, 1, @CalendarDate);
END;

-- Actual work is complete and up to date through Friday, June 12. Future dates remain planned only.
INSERT INTO Timesheet (ProjectId, TaskId, EmployeeId, EntryDate, HoursWorked)
SELECT allocation.ProjectId, allocation.TaskId, allocation.EmployeeId, calendar.WorkDate, allocation.HoursPerDay
FROM Allocation allocation
INNER JOIN @DemoTasks task ON task.ProjectId = allocation.ProjectId AND task.TaskId = allocation.TaskId
INNER JOIN #Calendar calendar ON calendar.WorkDate BETWEEN allocation.AllocationStartDate AND allocation.AllocationEndDate
WHERE calendar.WorkDate <= '2026-06-13'
  AND DATEDIFF(day, '19000101', calendar.WorkDate) % 7 NOT IN (5, 6);

-- Validation 1: every task is fully staffed for exactly its estimated effort.
IF EXISTS
(
    SELECT 1
    FROM TaskItem task
    INNER JOIN @DemoTasks demo ON demo.ProjectId = task.ProjectId AND demo.TaskId = task.TaskId
    OUTER APPLY
    (
        SELECT SUM(allocation.HoursPerDay) AS AllocatedHours
        FROM Allocation allocation
        INNER JOIN #Calendar calendar ON calendar.WorkDate BETWEEN allocation.AllocationStartDate AND allocation.AllocationEndDate
        WHERE allocation.ProjectId = task.ProjectId
          AND allocation.TaskId = task.TaskId
          AND DATEDIFF(day, '19000101', calendar.WorkDate) % 7 NOT IN (5, 6)
    ) total
    WHERE ABS(ISNULL(total.AllocatedHours, 0) - task.EstimatedHours) > 0.01
)
    THROW 51000, 'Demo data validation failed: task allocation totals do not match estimates.', 1;

-- Validation 2: daily combined allocations never exceed the employee work norm.
IF EXISTS
(
    SELECT 1
    FROM
    (
        SELECT allocation.EmployeeId, calendar.WorkDate, SUM(allocation.HoursPerDay) AS AllocatedHours
        FROM Allocation allocation
        INNER JOIN #Calendar calendar ON calendar.WorkDate BETWEEN allocation.AllocationStartDate AND allocation.AllocationEndDate
        WHERE DATEDIFF(day, '19000101', calendar.WorkDate) % 7 NOT IN (5, 6)
        GROUP BY allocation.EmployeeId, calendar.WorkDate
    ) load
    INNER JOIN Employee employee ON employee.EmployeeId = load.EmployeeId
    INNER JOIN WorkNorm norm ON norm.WorkNormId = employee.WorkNormId
    WHERE load.AllocatedHours > norm.HoursPerDay
)
    THROW 51001, 'Demo data validation failed: an employee exceeds the daily work norm.', 1;

-- Validation 3: assigned employees have the required skill at the same or a higher level.
IF EXISTS
(
    SELECT 1
    FROM Allocation allocation
    INNER JOIN @DemoTasks demo ON demo.ProjectId = allocation.ProjectId AND demo.TaskId = allocation.TaskId
    INNER JOIN TaskItem task ON task.ProjectId = allocation.ProjectId AND task.TaskId = allocation.TaskId
    INNER JOIN Skill requiredSkill ON requiredSkill.SkillId = task.RequiredSkillId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM EmployeeSkill employeeSkill
        INNER JOIN Skill skill ON skill.SkillId = employeeSkill.SkillId
        WHERE employeeSkill.EmployeeId = allocation.EmployeeId
          AND skill.SkillName = requiredSkill.SkillName
          AND CASE
                WHEN LOWER(skill.SkillLevel) LIKE '%senior%' THEN 3
                WHEN LOWER(skill.SkillLevel) LIKE '%mid%' OR LOWER(skill.SkillLevel) LIKE '%medium%' THEN 2
                WHEN LOWER(skill.SkillLevel) LIKE '%junior%' THEN 1
                ELSE 0
              END >= CASE
                WHEN LOWER(requiredSkill.SkillLevel) LIKE '%senior%' THEN 3
                WHEN LOWER(requiredSkill.SkillLevel) LIKE '%mid%' OR LOWER(requiredSkill.SkillLevel) LIKE '%medium%' THEN 2
                WHEN LOWER(requiredSkill.SkillLevel) LIKE '%junior%' THEN 1
                ELSE 0
              END
    )
)
    THROW 51002, 'Demo data validation failed: an allocation does not satisfy the task skill.', 1;

COMMIT TRANSACTION;
GO
