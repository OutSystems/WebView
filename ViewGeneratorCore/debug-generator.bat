@echo off
SET ts2langfile=%1
node --inspect-brk "%cd%\tools\node_modules\@outsystems\ts2lang\ts2lang-main.js" -f "%ts2langfile%" -t "%cd%\tools\dist\ViewGenerator.js"
