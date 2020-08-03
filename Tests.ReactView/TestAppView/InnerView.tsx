import * as React from 'react';

interface IInnerViewProperties {
    loaded: () => void;
    methodCalled: () => void;
}

interface IInnerViewBehaviors {
    testMethod(): void;
}

export default class InnerView extends React.Component<IInnerViewProperties, {}> implements IInnerViewBehaviors {

    componentDidMount() {
        this.props.loaded();
    }

    render() {
        return <div>"inner view"</div>;
    }

    testMethod() {
        this.props.methodCalled();
    }
}