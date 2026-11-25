#!/usr/bin/env python3
"""Local Windows attendance setup window; no employee data or secrets are displayed."""
import glob
import json
import os
import queue
import socket
import subprocess
import sys
import threading
from pathlib import Path
try:
    import tkinter as tk
    from tkinter import ttk, messagebox
except ImportError:
    raise SystemExit('Install Python with Tcl/Tk support to use this window. Start connector.cmd and Show computer IP.cmd remain available.')
import attendance_connector as connector

class SetupWindow:
    def __init__(self,root):
        self.root=root
        self.folder=Path(__file__).resolve().parent
        self.results=queue.Queue()
        self.scanning=False
        self.process=None
        self.rows=[]
        root.title('Garowe City | Attendance setup')
        root.geometry('850x650');root.minsize(680,610)
        style=ttk.Style(root);style.theme_use('clam')
        style.configure('.',font=('Segoe UI',10))
        style.configure('Title.TLabel',font=('Segoe UI',19,'bold'),foreground='#163a8a')
        style.configure('Heading.TLabel',font=('Segoe UI',11,'bold'))
        style.configure('Primary.TButton',foreground='white',background='#1846bc',padding=(14,9))
        style.map('Primary.TButton',background=[('active','#12358f'),('disabled','#8997b8')])
        style.configure('Treeview',rowheight=32)
        panel=ttk.Frame(root,padding=22);panel.pack(fill='both',expand=True)
        ttk.Label(panel,text='Garowe City Attendance',style='Title.TLabel').pack(anchor='w')
        ttk.Label(panel,text='Choose this computer’s Ethernet IP to connect your office terminal.').pack(anchor='w',pady=(5,16))
        config_row=ttk.Frame(panel);config_row.pack(fill='x')
        ttk.Label(config_row,text='Device configuration',style='Heading.TLabel').pack(side='left',padx=(0,12))
        self.config=tk.StringVar()
        self.config_box=ttk.Combobox(config_row,textvariable=self.config,state='readonly');self.config_box.pack(side='left',fill='x',expand=True)
        ttk.Button(config_row,text='Refresh files',command=self.refresh_configs).pack(side='left',padx=(8,0))
        ttk.Label(panel,text='Download the device configuration from Admin → Attendance devices into this extracted folder.',wraplength=760).pack(anchor='w',pady=(6,14))
        toolbar=ttk.Frame(panel);toolbar.pack(fill='x',pady=(0,8))
        self.scan_button=ttk.Button(toolbar,text='Scan computer IP',style='Primary.TButton',command=self.scan)
        self.scan_button.pack(side='left')
        ttk.Button(toolbar,text='Use selected IP',command=self.use_selected).pack(side='left',padx=8)
        self.progress=ttk.Progressbar(toolbar,mode='indeterminate',length=100);self.progress.pack(side='right')
        frame=ttk.Frame(panel);frame.pack(fill='both',expand=True)
        self.table=ttk.Treeview(frame,columns=('adapter','ip','subnet'),show='headings',selectmode='browse',height=6)
        for key,title,width in [('adapter','Network adapter',230),('ip','Current IPv4 address',180),('subnet','Subnet prefix',120)]:
            self.table.heading(key,text=title);self.table.column(key,width=width,minwidth=95)
        self.table.pack(side='left',fill='both',expand=True)
        scroll=ttk.Scrollbar(frame,orient='vertical',command=self.table.yview);scroll.pack(side='right',fill='y');self.table.configure(yscrollcommand=scroll.set)
        self.table.bind('<Double-1>',lambda event:self.use_selected())
        self.status=tk.StringVar(value='Ready to scan this computer.')
        ttk.Label(panel,textvariable=self.status,wraplength=760).pack(anchor='w',pady=(8,12))
        address_row=ttk.Frame(panel);address_row.pack(fill='x')
        ttk.Label(address_row,text='Connector IP',style='Heading.TLabel').pack(side='left',padx=(0,12))
        self.address=tk.StringVar();ttk.Entry(address_row,textvariable=self.address,width=22).pack(side='left')
        self.start_button=ttk.Button(address_row,text='Save & start connector',style='Primary.TButton',command=self.start)
        self.start_button.pack(side='right')
        self.instructions=tk.StringVar(value='Use Ethernet for the terminal cable; keep Wi-Fi online for cloud sync. Scanning does not change Windows network settings.')
        ttk.Label(panel,textvariable=self.instructions,wraplength=760).pack(anchor='w',pady=(14,0))
        self.refresh_configs();root.after(100,self.poll_results);root.after(200,self.scan)
    def refresh_configs(self):
        names=[Path(path).name for path in sorted(glob.glob(str(self.folder/'attendance-connector-*.json')))]
        self.config_box['values']=names
        if self.config.get() not in names:self.config.set(names[0] if names else '')
    def scan(self):
        if self.scanning:return
        self.scanning=True;self.scan_button.state(['disabled']);self.progress.start(12);self.status.set('Scanning this computer’s local adapters…')
        def worker():
            try:self.results.put((connector.computer_addresses(),None))
            except Exception:self.results.put(([], 'Could not read network adapters. Check the cable or enter the Ethernet IP manually.'))
        threading.Thread(target=worker,daemon=True).start()
    def poll_results(self):
        try:
            rows,error=self.results.get_nowait()
            self.rows=rows;self.table.delete(*self.table.get_children())
            for index,row in enumerate(rows):self.table.insert('', 'end',iid=str(index),values=(row['interface'],row['address'],'/'+str(row['prefix']) if row['prefix'] is not None else 'Unknown'))
            self.status.set(error or (str(len(rows))+' local addresses found. Select Ethernet, then click Use selected IP.' if rows else 'No private IPv4 address found. Connect/configure Ethernet, then click Scan computer IP again.'))
            self.scanning=False;self.progress.stop();self.scan_button.state(['!disabled'])
        except queue.Empty:pass
        self.root.after(100,self.poll_results)
    def use_selected(self):
        selected=self.table.selection()
        if not selected:
            messagebox.showinfo('Choose an adapter','Select the Ethernet adapter from the list first.');return
        row=self.rows[int(selected[0])];self.address.set(row['address'])
        self.status.set('Selected '+row['interface']+' · '+row['address']+'. Save & start when ready.')
    def start(self):
        if self.process is not None and self.process.poll() is None:
            messagebox.showinfo('Connector running','The connector is already running. Keep its console window open.');return
        path=self.folder/self.config.get()
        if path.parent!=self.folder or not path.is_file():
            messagebox.showinfo('Device configuration needed','Download this device’s connector configuration into this folder, then click Refresh files.');return
        try:
            address=self.address.get().strip()
            config=connector.save_listen_address(str(path),address)
            try:
                with socket.create_connection((address,config['port']),timeout=.4):
                    messagebox.showinfo('Port already in use','A listener already uses this address and port. Check the existing connector console before starting another.');return
            except OSError:pass
            self.process=subprocess.Popen([sys.executable,'-u',str(self.folder/'attendance_connector.py'),'--config',str(path)],cwd=self.folder,creationflags=getattr(subprocess,'CREATE_NEW_CONSOLE',0))
            self.instructions.set('Terminal ADMS server: '+address+' · port '+str(config['port'])+' · HTTPS OFF · domain OFF. Keep the connector console open and this computer awake. Make a real scan and refresh device status online.')
            self.status.set('Connector console opened. Check its messages, then verify a real attendance scan in the online system.')
        except (OSError,ValueError,KeyError):
            messagebox.showerror('Cannot start connector','Select an IP assigned to this computer and a valid downloaded device configuration. Network settings were not changed.')

def main():
    root=tk.Tk();SetupWindow(root);root.mainloop()
if __name__=='__main__':main()
