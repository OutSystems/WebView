import * as React from 'react';

interface IInnerViewProperties {
    loaded: () => void;
}

export default class InnerView extends React.Component<IInnerViewProperties, {}> {

    componentDidMount() {
        this.props.loaded();
    }

    render() {
        return <div>"inner view"</div>;
    }
}