enum Operation {
    Added,
    Removed
}

type CollectionChangedListener<T> = (item: T, operation: Operation) => void;

export abstract class ObservableCollection<T> {

    private listeners: CollectionChangedListener<T>[] = [];

    public add(item: T): void {
        this.triggerCollectionChangedListeners(item, Operation.Added);
    }

    public remove(item: T): void {
        this.triggerCollectionChangedListeners(item, Operation.Removed);
    }

    public abstract get items(): T[];

    public addChangedListener(listener: CollectionChangedListener<T>) {
        this.listeners.push(listener);
    }

    private triggerCollectionChangedListeners(item: T, operation: Operation) {
        this.listeners.forEach(l => l(item, operation));
    }
}

export class ObservableListCollection<T> extends ObservableCollection<T> {

    private list: T[] = [];

    public add(item: T): void {
        this.list.push(item);
        super.add(item);
    }

    public remove(item: T): void {
        this.list.splice(this.list.indexOf(item), 1);
        super.remove(item);
    }

    public get items(): T[] {
        return this.list;
    }
}