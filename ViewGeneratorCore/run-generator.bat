@echo off
SET ts2langfile=%1
node "%cd%\tools\node_modules\@outsystems\ts2lang\ts2lang-main.js" -f "%ts2langfile%" -t "%cd%\tools\dist\ViewGenerator.js"
