namespace ReactViewControl {

    public interface IChildViewHost {

        void AttachChildView(IViewModule viewModule, string frameName);

        T WithPlugin<T>(string frameName = ReactViewRender.MainViewFrameName);
    }
}
