import { useEffect, useLayoutEffect, useRef } from 'react';
import { basicSetup } from 'codemirror';
import { Annotation, Compartment, EditorState, Transaction } from '@codemirror/state';
import { EditorView, keymap } from '@codemirror/view';
import { indentWithTab } from '@codemirror/commands';
import { html } from '@codemirror/lang-html';
import { css } from '@codemirror/lang-css';

const externalChange = Annotation.define<boolean>();

type Props = {
  id: string;
  label: string;
  language: 'html' | 'css';
  value: string;
  disabled: boolean;
  maxLength: number;
  onChange: (value: string) => void;
};

export default function CodeEditor({ id, label, language, value, disabled, maxLength, onChange }: Props) {
  const container = useRef<HTMLDivElement>(null);
  const editor = useRef<EditorView | null>(null);
  const editable = useRef(new Compartment());
  const changeHandler = useRef(onChange);

  useLayoutEffect(() => { changeHandler.current = onChange; }, [onChange]);

  useEffect(() => {
    const view = new EditorView({
      parent: container.current!,
      extensions: [
        basicSetup,
        keymap.of([indentWithTab]),
        language === 'html' ? html() : css(),
        EditorView.lineWrapping,
        EditorView.contentAttributes.of({ id, 'aria-labelledby': `${id}-label`, 'aria-describedby': 'editor-keyboard-help' }),
        editable.current.of([]),
        EditorState.changeFilter.of(transaction => !transaction.docChanged || transaction.newDoc.length <= maxLength),
        EditorView.updateListener.of(update => {
          if (update.docChanged && !update.transactions.some(transaction => transaction.annotation(externalChange))) {
            changeHandler.current(update.state.doc.toString());
          }
        }),
      ],
    });
    editor.current = view;
    return () => {
      view.destroy();
      editor.current = null;
    };
  }, [id, language, maxLength]);

  useEffect(() => {
    const view = editor.current!;
    if (view.state.doc.toString() !== value) {
      view.dispatch({
        changes: { from: 0, to: view.state.doc.length, insert: value },
        annotations: [externalChange.of(true), Transaction.addToHistory.of(false)],
      });
    }
  }, [value, id, language, maxLength]);

  useEffect(() => {
    // A disabled fieldset does not disable CodeMirror.
    editor.current!.dispatch({
      effects: editable.current.reconfigure([
        EditorState.readOnly.of(disabled),
        EditorView.editable.of(!disabled),
        EditorView.contentAttributes.of({ 'aria-disabled': String(disabled) }),
      ]),
    });
  }, [disabled, id, language, maxLength]);

  return <div>
    <p id={`${id}-label`}>{label}</p>
    <div ref={container} />
  </div>;
}
