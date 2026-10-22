import { fireEvent, render, screen } from '@testing-library/react';
import { createElement } from 'react';
import { afterEach, describe, expect, it } from 'vitest';
import { Button } from '../components/Button';
import { installTapFeedback } from './tapFeedback';

let uninstall: () => void = () => undefined;

afterEach(() => {
  uninstall();
  document.body.replaceChildren();
});

function button(attributes: Record<string, string> = {}): HTMLButtonElement {
  const element = document.createElement('button');
  element.className = 'app-button';
  element.textContent = 'แสดง';
  for (const [name, value] of Object.entries(attributes)) {
    element.setAttribute(name, value);
  }
  document.body.append(element);
  return element;
}

describe('tapFeedback', () => {
  it('a tap plays the press once, to the end, however short the tap', () => {
    uninstall = installTapFeedback(document);
    const show = button();

    fireEvent.pointerDown(show);
    fireEvent.pointerUp(show);
    expect(show).toHaveClass('is-tapped');

    fireEvent.animationEnd(show);
    expect(show).not.toHaveClass('is-tapped');
  });

  it('a tap on the text inside a button-styled link counts too', () => {
    uninstall = installTapFeedback(document);
    const link = document.createElement('a');
    link.className = 'app-button';
    link.href = '/admin/export';
    const label = document.createElement('span');
    label.textContent = 'ส่งออก Excel';
    link.append(label);
    document.body.append(link);

    fireEvent.pointerDown(label);

    expect(link).toHaveClass('is-tapped');
  });

  it('a disabled button, or anything that is not a button-styled element, plays nothing', () => {
    uninstall = installTapFeedback(document);
    const busy = button({ disabled: '' });
    const other = document.createElement('button');
    document.body.append(other);

    fireEvent.pointerDown(busy);
    fireEvent.pointerDown(other);

    expect(busy).not.toHaveClass('is-tapped');
    expect(other).not.toHaveClass('is-tapped');
  });

  it('a tap on the new-look Button plays the press as well (facility-0087)', () => {
    uninstall = installTapFeedback(document);
    render(createElement(Button, { variant: 'primary' }, 'ส่งงาน'));
    const send = screen.getByRole('button', { name: 'ส่งงาน' });

    fireEvent.pointerDown(send);

    expect(send).toHaveClass('is-tapped');
  });

  it('stops listening once uninstalled', () => {
    installTapFeedback(document)();
    const show = button();

    fireEvent.pointerDown(show);

    expect(show).not.toHaveClass('is-tapped');
  });
});
