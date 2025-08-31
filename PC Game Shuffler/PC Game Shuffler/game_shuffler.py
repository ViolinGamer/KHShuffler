
# ---------------- Entry ----------------
if __name__ == "__main__":
    root = tk.Tk()
    root.minsize(1060, 640)
    app = GameShuffler(root)
    try:
        root.protocol("WM_DELETE_WINDOW", app.on_close)
    except Exception:
        pass
    root.mainloop()
