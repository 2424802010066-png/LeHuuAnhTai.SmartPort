-- =====================================================================
-- SUA LOI AM THAM: Container.TinhTrang la varchar(10) (KHONG Unicode).
-- Ghi "Rỗng" vao do -> SQL Server luu thanh "R?ng", KHONG bao loi gi ca.
-- Du lieu cu deu la Empty/Full (ASCII) nen bay gio moi lo ra.
--
-- Cot nay co rang buoc DEFAULT (ten tu sinh, khac nhau tren tung CSDL) nen
-- phai GO rang buoc -> doi kieu -> GAN LAI. Doi thang se bi tu choi.
-- =====================================================================

IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID('Container') AND c.name = 'TinhTrang' AND t.name = 'varchar')
BEGIN
    DECLARE @rb SYSNAME, @mac NVARCHAR(400);

    SELECT @rb = dc.name, @mac = dc.definition
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('Container') AND c.name = 'TinhTrang';

    IF @rb IS NOT NULL
        EXEC('ALTER TABLE Container DROP CONSTRAINT [' + @rb + ']');

    ALTER TABLE Container ALTER COLUMN TinhTrang NVARCHAR(20) NULL;

    IF @mac IS NOT NULL
        EXEC('ALTER TABLE Container ADD CONSTRAINT [DF_Container_TinhTrang] DEFAULT ' + @mac + ' FOR TinhTrang');
END

-- Chuan hoa gia tri da bi hong (neu co) ve dung mot bo Empty/Full
IF EXISTS (SELECT 1 FROM Container WHERE TinhTrang IN ('R?ng', N'Rỗng'))
    UPDATE Container SET TinhTrang = 'Empty' WHERE TinhTrang IN ('R?ng', N'Rỗng');
