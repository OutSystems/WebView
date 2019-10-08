param($installPath, $toolsPath, $package, $project)
$projectFullName = $project.FullName
Write-Host "Copying Resources folder to the root of the project: $projectFullName"
 
$fileInfo = new-object -typename System.IO.FileInfo -ArgumentList $projectFullName
$projectDirectory = $fileInfo.DirectoryName
$sourceDirectory = "$installPath\contentFiles"
$destinationDirectory = "$projectDirectory"
 
if(test-path $sourceDirectory -pathtype container)
{
 Write-Host "Copying files from $sourceDirectory to $destinationDirectory"
 robocopy $sourceDirectory $destinationDirectory /e /XO
}
 
Write-Host "Copying complete. $projectFullName"