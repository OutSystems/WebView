namespace ReactViewControl {
    interface IDependenciesProvider {

        string[] GetCssDependencies(string moduleName);

        string[] GetJsDependencies(string moduleName);
    }
}