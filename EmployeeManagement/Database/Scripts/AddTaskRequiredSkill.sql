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
