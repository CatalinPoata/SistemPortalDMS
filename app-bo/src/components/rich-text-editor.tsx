"use client";

import Link from "@tiptap/extension-link";
import { EditorContent, useEditor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import { useEffect } from "react";

const toolbarButtonClass =
  "rounded border px-2 py-1 text-sm font-medium disabled:cursor-not-allowed " +
  "disabled:opacity-50";

const inactiveToolbarButtonClass =
  "border-slate-300 bg-white text-slate-800 hover:bg-slate-50";

const activeToolbarButtonClass =
  "border-blue-700 bg-blue-700 text-white hover:bg-blue-800";

function ToolbarButton({
  active = false,
  children,
  disabled,
  onClick,
  title,
}: {
  active?: boolean;
  children: React.ReactNode;
  disabled: boolean;
  onClick: () => void;
  title: string;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      title={title}
      aria-label={title}
      aria-pressed={active}
      className={`${toolbarButtonClass} ${
        active ? activeToolbarButtonClass : inactiveToolbarButtonClass
      }`}
      onMouseDown={event => event.preventDefault()}
      onClick={onClick}
    >
      {children}
    </button>
  );
}

export function RichTextEditor({
  value,
  disabled,
  onChange,
}: {
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit.configure({
        heading: { levels: [2, 3] },
        code: false,
        codeBlock: false,
        horizontalRule: false,
        strike: false,
      }),
      Link.configure({
        openOnClick: false,
        autolink: true,
        linkOnPaste: true,
        HTMLAttributes: {
          rel: "noopener noreferrer",
          target: "_blank",
        },
        isAllowedUri: url => /^(https?:|mailto:)/i.test(url),
      }),
    ],
    content: value,
    editable: !disabled,
    editorProps: {
      attributes: {
        class:
          "rich-text-editor-content min-h-52 p-3 outline-none focus:ring-2 " +
          "focus:ring-inset focus:ring-blue-200",
        "aria-label": "Conținut articol",
      },
    },
    onUpdate: ({ editor: activeEditor }) => onChange(activeEditor.getHTML()),
  });

  useEffect(() => {
    if (!editor) return;

    editor.setEditable(!disabled);
  }, [disabled, editor]);

  useEffect(() => {
    if (!editor || editor.getHTML() === value) return;

    editor.commands.setContent(value, { emitUpdate: false });
  }, [editor, value]);

  function editLink() {
    if (!editor) return;

    const currentHref = editor.getAttributes("link").href as string | undefined;
    const href = window.prompt(
      "Adresa linkului (https://… sau mailto:…)",
      currentHref ?? "https://",
    );

    if (href === null) return;

    if (href.trim() === "") {
      editor.chain().focus().unsetLink().run();
      return;
    }

    if (!/^(https?:|mailto:)/i.test(href.trim())) {
      window.alert("Sunt permise doar linkuri http, https sau mailto.");
      return;
    }

    editor.chain().focus().setLink({ href: href.trim() }).run();
  }

  const editorDisabled = disabled || !editor;

  return (
    <div className="rich-text-editor mt-1 overflow-hidden rounded-md border border-slate-300 bg-white">
      <div
        className="flex flex-wrap gap-2 border-b border-slate-200 p-2"
        role="toolbar"
        aria-label="Formatare conținut articol"
      >
        <ToolbarButton
          disabled={editorDisabled}
          active={editor?.isActive("paragraph")}
          title="Paragraf"
          onClick={() => editor?.chain().focus().setParagraph().run()}
        >
          P
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled}
          active={editor?.isActive("heading", { level: 2 })}
          title="Titlu nivel 2"
          onClick={() => editor?.chain().focus().toggleHeading({ level: 2 }).run()}
        >
          H2
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled}
          active={editor?.isActive("heading", { level: 3 })}
          title="Titlu nivel 3"
          onClick={() => editor?.chain().focus().toggleHeading({ level: 3 }).run()}
        >
          H3
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled}
          active={editor?.isActive("bold")}
          title="Aldin"
          onClick={() => editor?.chain().focus().toggleBold().run()}
        >
          B
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled}
          active={editor?.isActive("italic")}
          title="Cursiv"
          onClick={() => editor?.chain().focus().toggleItalic().run()}
        >
          I
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled}
          active={editor?.isActive("bulletList")}
          title="Listă cu marcatori"
          onClick={() => editor?.chain().focus().toggleBulletList().run()}
        >
          • Listă
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled}
          active={editor?.isActive("orderedList")}
          title="Listă numerotată"
          onClick={() => editor?.chain().focus().toggleOrderedList().run()}
        >
          1. Listă
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled}
          active={editor?.isActive("blockquote")}
          title="Citat"
          onClick={() => editor?.chain().focus().toggleBlockquote().run()}
        >
          Citat
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled}
          active={editor?.isActive("link")}
          title="Adaugă sau modifică link"
          onClick={editLink}
        >
          Link
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled || !editor?.isActive("link")}
          title="Elimină link"
          onClick={() => editor?.chain().focus().unsetLink().run()}
        >
          Elimină link
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled}
          title="Curăță formatarea blocului sau selecției"
          onClick={() => editor?.chain().focus().unsetAllMarks().clearNodes().run()}
        >
          Curăță formatarea
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled || !editor?.can().chain().focus().undo().run()}
          title="Anulează"
          onClick={() => editor?.chain().focus().undo().run()}
        >
          Anulează
        </ToolbarButton>
        <ToolbarButton
          disabled={editorDisabled || !editor?.can().chain().focus().redo().run()}
          title="Repetă"
          onClick={() => editor?.chain().focus().redo().run()}
        >
          Repetă
        </ToolbarButton>
      </div>
      <EditorContent editor={editor} />
    </div>
  );
}
