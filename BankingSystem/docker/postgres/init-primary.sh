# สร้าง replication user + slot สำหรับ replica

set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    -- สร้าง user สำหรับ replication
    CREATE USER replicator WITH REPLICATION ENCRYPTED PASSWORD 'replicator_password';

    -- สร้าง replication slot (ป้องกัน WAL ถูกลบก่อน replica อ่านเสร็จ)
    SELECT pg_create_physical_replication_slot('replica_slot_1');

    -- สร้าง read-only user สำหรับ app อ่านจาก replica
    CREATE USER readonly WITH PASSWORD 'readonly_password';
    GRANT CONNECT ON DATABASE banking_db TO readonly;
    GRANT USAGE ON SCHEMA public TO readonly;
    GRANT SELECT ON ALL TABLES IN SCHEMA public TO readonly;
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO readonly;
EOSQL

echo "Primary initialization completed."