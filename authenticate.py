
import os
import sys

def main():
    exit_code = os.system("nuget.exe sources update -Name \"Azure\" -Username \"Azure\" -Password \"" + sys.argv[1] + "\" -StorePasswordInClearText -ConfigFile NuGet.config")
    if exit_code != 0:
        exit_code = os.system("nuget.exe sources add -Name \"Azure\" -Username \"Azure\" -Password \""+sys.argv[1]+"\" -StorePasswordInClearText -source \"https://pkgs.dev.azure.com/OutSystemsRD/_packaging/ArtifactRepository/nuget/v3/index.json\"")

if __name__== "__main__":
  main()
