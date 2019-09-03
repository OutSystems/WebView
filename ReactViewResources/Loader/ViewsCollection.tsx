import * as React from "react";

export class ViewsCollection extends React.Component<{}, { views: React.ReactNode[] }> {

    constructor(props: {}) {
        super(props);
        this.state = {
            views: []
        };
    }

    public addView(view: React.ReactNode) {
        this.setState(s => {
            debugger;
            return { views: s.views.concat(view) };
        });
    }

    public removeView(view: React.ReactNode) {
        //this.setState(s => {
        //    views: s.views.splice(s.views.findIndex(i => i === view), 1)
        //});
    }

    public render(): JSX.Element {
        return <>{this.state.views}</>;
    }
}