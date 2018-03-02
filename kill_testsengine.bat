taskkill /F /IM vstest.discoveryengine.x86.exe /FI "MEMUSAGE gt 1" 2>NUL 
taskkill /F /IM vstest.executionengine.x86.exe /FI "MEMUSAGE gt 1" 2>NUL 