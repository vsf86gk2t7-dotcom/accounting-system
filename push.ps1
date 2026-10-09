param(
    [string]$Message = "update: changes"
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Git Push Helper" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Show changed files
Write-Host "[1/4] Changed files:" -ForegroundColor Yellow
git status --short
Write-Host ""

# 2. Add
Write-Host "[2/4] Adding files..." -ForegroundColor Cyan
git add .
Write-Host "      OK" -ForegroundColor Green
Write-Host ""

# 3. Commit
Write-Host "[3/4] Committing: $Message" -ForegroundColor Yellow
git commit -m $Message
Write-Host ""

# 4. Push
Write-Host "[4/4] Pushing to GitHub..." -ForegroundColor Green
git push
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Done!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""