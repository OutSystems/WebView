@echo off
SET /P ts2langfile=Specify a path to a ts2lang json file: 
node --inspect-brk "%cd%\tools\node_modules\@outsystems\ts2lang\ts2lang-main.js" -f "%ts2langfile%" -t "%cd%\tools\dist\ViewGenerator.js"