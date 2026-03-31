# Clone data จาก Primary แล้วตั้งค่าเป็น standby

set -e

# ถ้ามี data อยู่แล้ว → ข้าม (ไม่ init ซ้ำ)
if [ -s "/var/lib/postgresql/data/PG_VERSION" ]; then
    echo "Replica data already exists, skipping init."
    exit 0
fi

# รอ Primary พร้อม
until pg_isready -h postgres-primary -U postgres; do
    echo "Waiting for primary..."
    sleep 2
done

# ลบ data directory เดิม (ถ้ามี)
rm -rf /var/lib/postgresql/data/*

# Clone จาก Primary ด้วย pg_basebackup
pg_basebackup \
    -h postgres-primary \
    -U replicator \
    -D /var/lib/postgresql/data \
    -Fp -Xs -P -R \
    -S replica_slot_1

# -Fp: plain format
# -Xs: stream WAL ระหว่าง backup
# -P: show progress
# -R: สร้าง standby.signal + primary_conninfo อัตโนมัติ

echo "Replica initialization completed."