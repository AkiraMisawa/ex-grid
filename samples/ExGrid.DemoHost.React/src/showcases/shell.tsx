// The frame every ExChart Showcase shares (ADR-0280): ExChart.Showcases' ShowcaseShell under the built-in Chrome,
// an application bar with the application's name and the screen's, and the screen below it. Its stylesheet is
// the Razor component's own, read from it, so the three hosts dress the Showcases from one record.
import type { ReactElement, ReactNode } from 'react';
import shellSource from '../../../../dotnet/samples/ExChart.Showcases/ShowcaseShell.razor?raw';

/** The stylesheet a Showcase's Razor component holds in its `<style>`, so the React host dresses it from the same rules. */
export function razorStyle(source: string, component: string): string {
    const style = /<style>([\s\S]*?)<\/style>/.exec(source);
    if (style?.[1] === undefined) throw new Error(`${component} holds no <style>, so its Showcase has no stylesheet.`);
    return style[1];
}

const shellStyle = razorStyle(shellSource, 'ShowcaseShell.razor');

/** One Showcase's frame: the application's name, where the screen sits, the bar's controls and the screen. */
export function ShowcaseShell({ app, crumb, tools, children }: {
    readonly app: string;
    readonly crumb: string;
    readonly tools?: ReactNode;
    readonly children: ReactNode;
}): ReactElement {
    return (
        <div className="xs-app xs-builtin">
            <style>{shellStyle}</style>
            <header className="xs-bar">
                <span className="xs-mark">{app.slice(0, 1)}</span>
                <span className="xs-name">{app}</span>
                <span className="xs-crumb">{crumb}</span>
                <span className="xs-spacer" />
                {tools}
            </header>
            {children}
        </div>
    );
}

/** A switch between views, as the Showcases' bars draw one under the built-in Chrome. */
export function Views<View extends string>({ label, views, value, onChange, className = 'xs-views' }: {
    readonly label: string;
    readonly views: readonly View[];
    readonly value: View;
    readonly onChange: (view: View) => void;
    readonly className?: string;
}): ReactElement {
    return (
        <nav className={className} aria-label={label}>
            {views.map(view => (
                <button key={view} type="button" className={view === value ? 'xs-view xs-view-on' : 'xs-view'}
                    aria-pressed={view === value} onClick={() => { onChange(view); }}>{view}</button>
            ))}
        </nav>
    );
}
