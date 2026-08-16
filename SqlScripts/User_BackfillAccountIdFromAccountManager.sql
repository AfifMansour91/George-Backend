-- Backfill User.AccountId for account managers that were silently detached from their account.
--
-- Background: UserStorage.UpdateUserAsync used to apply AccountId unconditionally, so any caller
-- passing a partial User (AccountId = NULL) wiped the user's account link. An account_admin with
-- AccountId = NULL bypasses all tenant scoping in the SPA (sees every site in the system).
-- The code fix makes detach explicit (detachFromAccount flag); this script repairs existing rows
-- using Account.ManagerId as the source of truth.
--
-- Safe to re-run (idempotent): only touches users whose AccountId is currently NULL.

-- 1) Preview the affected rows first:
SELECT u.Id AS UserId, u.Email, u.RoleId, u.AccountId AS CurrentAccountId,
       a.Id AS AccountIdFromManager, a.Name AS AccountName
FROM [User] u
JOIN Account a ON a.ManagerId = u.Id AND a.IsDeleted = 0
WHERE u.IsDeleted = 0
  AND u.AccountId IS NULL;

-- 2) Apply the backfill:
UPDATE u
SET u.AccountId = a.Id,
    u.UpdatedDate = GETUTCDATE()
FROM [User] u
JOIN Account a ON a.ManagerId = u.Id AND a.IsDeleted = 0
WHERE u.IsDeleted = 0
  AND u.AccountId IS NULL;

-- 3) Verify no account manager is left without an account link:
SELECT u.Id AS UserId, u.Email, u.RoleId, u.AccountId
FROM [User] u
JOIN Account a ON a.ManagerId = u.Id AND a.IsDeleted = 0
WHERE u.IsDeleted = 0
  AND u.AccountId IS NULL;
