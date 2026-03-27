# Version Control for DevOps

> Git + VCS Hosting (GitHub, GitLab, Bitbucket) — เนื้อหาหลักอยู่ใน 03-Version-Control-Systems แล้ว ส่วนนี้เสริมมุม DevOps

---

## 1. Git สำหรับ DevOps

```
สิ่งที่ DevOps ต้องรู้เพิ่มจาก Developer:

├── Git Hooks          — รัน script อัตโนมัติก่อน/หลัง commit
├── Git Submodules     — จัดการ repo ภายใน repo
├── Monorepo vs Polyrepo — กลยุทธ์จัดการ repos
├── GitOps             — ใช้ Git เป็น source of truth สำหรับ infrastructure
└── Branch Protection  — ตั้งกฎป้องกัน branch
```

### Git Hooks

```bash
# .git/hooks/pre-commit — รันก่อน commit
#!/bin/bash
echo "Running linter..."
npm run lint
if [ $? -ne 0 ]; then
    echo "❌ Lint failed. Fix errors before committing."
    exit 1
fi

# .git/hooks/pre-push — รันก่อน push
#!/bin/bash
echo "Running tests..."
npm test
if [ $? -ne 0 ]; then
    echo "❌ Tests failed. Fix before pushing."
    exit 1
fi
```

### Monorepo vs Polyrepo

```
Monorepo (1 repo = ทุก services):
├── /services/auth/
├── /services/orders/
├── /services/payments/
├── /shared/libs/
└── /infrastructure/
✅ Share code ง่าย, atomic changes
❌ Repo ใหญ่, CI ช้า, permission ซับซ้อน
ใช้โดย: Google, Meta, Uber

Polyrepo (1 repo = 1 service):
repo: auth-service
repo: orders-service
repo: payments-service
repo: infra-configs
✅ แยกชัดเจน, CI เร็ว, permission ง่าย
❌ Share code ยาก, version management ซับซ้อน
ใช้โดย: Netflix, Amazon
```

## 2. VCS Hosting สำหรับ DevOps

```
| Platform   | CI/CD             | เด่นเรื่อง                    |
|-----------|-------------------|-------------------------------|
| GitHub    | GitHub Actions    | Community ใหญ่ที่สุด, open source|
| GitLab    | GitLab CI/CD      | DevOps ครบในตัว, self-hosted   |
| Bitbucket | Bitbucket Pipelines| Jira integration, Atlassian    |
```
