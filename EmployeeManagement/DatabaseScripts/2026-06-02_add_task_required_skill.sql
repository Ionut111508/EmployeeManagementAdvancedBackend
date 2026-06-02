IF COL_LENGTH('TaskItem', 'RequiredSkillId') IS NULL
BEGIN
    ALTER TABLE [TaskItem]
    ADD [RequiredSkillId] VARCHAR(50) NULL;
END
GO

IF COL_LENGTH('TaskItem', 'RequiredSkillId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = 'FK_TaskItem_RequiredSkill'
         AND parent_object_id = OBJECT_ID('TaskItem')
   )
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_TaskItem_RequiredSkillId'
          AND object_id = OBJECT_ID('TaskItem')
    )
    BEGIN
        DROP INDEX [IX_TaskItem_RequiredSkillId] ON [TaskItem];
    END

    ALTER TABLE [TaskItem]
    ALTER COLUMN [RequiredSkillId] VARCHAR(50) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_TaskItem_RequiredSkill'
      AND parent_object_id = OBJECT_ID('TaskItem')
)
BEGIN
    ALTER TABLE [TaskItem]
    ADD CONSTRAINT [FK_TaskItem_RequiredSkill]
        FOREIGN KEY ([RequiredSkillId]) REFERENCES [Skill]([SkillId]);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_TaskItem_RequiredSkillId'
      AND object_id = OBJECT_ID('TaskItem')
)
BEGIN
    CREATE INDEX [IX_TaskItem_RequiredSkillId]
    ON [TaskItem]([RequiredSkillId]);
END
GO

;WITH RankedSkills AS
(
    SELECT
        SkillId,
        ROW_NUMBER() OVER (
            PARTITION BY SkillName
            ORDER BY CASE
                WHEN LOWER(ISNULL(SkillLevel, '')) LIKE '%mid%' OR LOWER(ISNULL(SkillLevel, '')) LIKE '%mediu%' OR LOWER(ISNULL(SkillLevel, '')) LIKE '%medium%' THEN 1
                WHEN LOWER(ISNULL(SkillLevel, '')) LIKE '%junior%' THEN 2
                WHEN LOWER(ISNULL(SkillLevel, '')) LIKE '%senior%' OR LOWER(ISNULL(SkillLevel, '')) LIKE '%expert%' THEN 3
                ELSE 4
            END
        ) AS RowNumber
    FROM [Skill]
)
UPDATE t
SET RequiredSkillId = s.SkillId
FROM [TaskItem] t
CROSS APPLY (
    SELECT TOP 1 SkillId
    FROM RankedSkills
    WHERE RowNumber = 1
    ORDER BY SkillId
) s
WHERE t.RequiredSkillId IS NULL
  AND EXISTS (SELECT 1 FROM [Skill]);
GO
